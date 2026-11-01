using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;

namespace Jinaga.Tool;

/// <summary>
/// Resolves the dependencies of a rules assembly the tool loads from outside
/// its own directory.
/// </summary>
/// <remarks>
/// The tool ships no copy of Jinaga, so the runtime has to find the one the
/// rules assembly was built against. An application's build output carries its
/// dependencies beside it, and a class library's does not: by default
/// <c>dotnet build</c> leaves NuGet assets in the global packages folder and
/// records where they live in the library's <c>.deps.json</c>. This resolver
/// reads that file, so a class library built with default settings works as
/// built.
/// </remarks>
internal sealed class RulesAssemblyDependencyResolver
{
    private readonly string rulesAssemblyPath;
    private readonly string rulesAssemblyDirectory;
    private readonly string packagesFolder;
    private readonly Func<string, bool> fileExists;
    private readonly Lazy<IReadOnlyDictionary<string, string>> runtimeAssets;

    private static readonly object registrationLock = new();
    private static readonly HashSet<string> registeredPaths = new(StringComparer.Ordinal);

    public RulesAssemblyDependencyResolver(
        string rulesAssemblyPath,
        string? packagesFolder = null,
        Func<string, bool>? fileExists = null)
    {
        this.rulesAssemblyPath = Path.GetFullPath(rulesAssemblyPath);
        this.rulesAssemblyDirectory = Path.GetDirectoryName(this.rulesAssemblyPath)
            ?? Directory.GetCurrentDirectory();
        this.packagesFolder = packagesFolder ?? DefaultPackagesFolder();
        this.fileExists = fileExists ?? File.Exists;
        this.runtimeAssets = new Lazy<IReadOnlyDictionary<string, string>>(ReadRuntimeAssets);
    }

    /// <summary>
    /// The <c>.deps.json</c> beside the rules assembly, whether or not it exists.
    /// </summary>
    public string DependencyFilePath =>
        Path.Combine(
            rulesAssemblyDirectory,
            Path.GetFileNameWithoutExtension(rulesAssemblyPath) + ".deps.json");

    /// <summary>
    /// The NuGet global packages folder: <c>NUGET_PACKAGES</c> when it is set,
    /// and <c>~/.nuget/packages</c> otherwise.
    /// </summary>
    public static string DefaultPackagesFolder()
    {
        var configured = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(profile, ".nuget", "packages");
    }

    /// <summary>
    /// Hooks this resolver onto the default load context and leaves it there.
    /// A rules assembly's dependencies load lazily, when a JinagaConfig method
    /// is invoked, so the hook has to outlive the load that installed it.
    /// </summary>
    public static RulesAssemblyDependencyResolver Register(string rulesAssemblyPath)
    {
        var resolver = new RulesAssemblyDependencyResolver(rulesAssemblyPath);
        lock (registrationLock)
        {
            if (registeredPaths.Add(resolver.rulesAssemblyPath))
            {
                AssemblyLoadContext.Default.Resolving += resolver.OnResolving;
            }
        }
        return resolver;
    }

    /// <summary>
    /// Every assembly the rules assembly's <c>.deps.json</c> places in the
    /// global packages folder rather than beside the assembly itself.
    /// </summary>
    public IReadOnlyCollection<string> PackagedAssemblyNames =>
        runtimeAssets.Value.Keys.ToList();

    /// <summary>
    /// Every place this resolver looks for an assembly, in the order it looks.
    /// </summary>
    public IReadOnlyList<string> ProbePaths(string assemblySimpleName)
    {
        var paths = new List<string>
        {
            Path.Combine(rulesAssemblyDirectory, assemblySimpleName + ".dll")
        };

        if (runtimeAssets.Value.TryGetValue(assemblySimpleName, out var relativePath))
        {
            paths.Add(Path.Combine(packagesFolder, relativePath));
        }

        return paths;
    }

    /// <summary>
    /// The first probe path that exists, or null when none does.
    /// </summary>
    public string? Resolve(string assemblySimpleName)
    {
        foreach (var path in ProbePaths(assemblySimpleName))
        {
            if (fileExists(path))
            {
                return path;
            }
        }

        return null;
    }

    /// <summary>
    /// What to tell the author when an assembly is in neither place: which
    /// assembly is missing, where the tool looked, and what to do about it.
    /// </summary>
    public string DescribeFailure(string assemblySimpleName)
    {
        var message = new StringBuilder();
        message.AppendLine(
            $"Could not find the assembly {assemblySimpleName}, which " +
            $"{Path.GetFileName(rulesAssemblyPath)} needs. Tried:");

        foreach (var path in ProbePaths(assemblySimpleName))
        {
            message.AppendLine($"  {path}");
        }

        if (!runtimeAssets.Value.ContainsKey(assemblySimpleName))
        {
            message.AppendLine(fileExists(DependencyFilePath)
                ? $"  {DependencyFilePath} lists no runtime asset for {assemblySimpleName}."
                : $"  {DependencyFilePath} is not there, so no package asset could be located.");
        }

        message.AppendLine(
            "Restore the project that produced the rules assembly, so that its packages are " +
            "in the NuGet global packages folder.");
        message.Append(
            "Or add <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies> to that " +
            "project and rebuild, so that its dependencies sit beside it.");

        return message.ToString();
    }

    private Assembly? OnResolving(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        var simpleName = assemblyName.Name;
        if (string.IsNullOrEmpty(simpleName))
        {
            return null;
        }

        // A satellite assembly is absent whenever a culture is not translated,
        // and the runtime expects to be told so rather than to fail.
        if (simpleName.EndsWith(".resources", StringComparison.Ordinal))
        {
            return null;
        }

        var path = Resolve(simpleName);
        if (path != null)
        {
            return context.LoadFromAssemblyPath(path);
        }

        throw new FileNotFoundException(DescribeFailure(simpleName));
    }

    /// <summary>
    /// Maps each assembly the rules assembly's <c>.deps.json</c> lists to its
    /// path relative to the global packages folder. Entries without a library
    /// path are left out: a project's own output is copied beside the rules
    /// assembly, where the first probe path already looks.
    /// </summary>
    private IReadOnlyDictionary<string, string> ReadRuntimeAssets()
    {
        var assets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var dependencyFilePath = DependencyFilePath;
        if (!fileExists(dependencyFilePath))
        {
            return assets;
        }

        JsonDocument document;
        try
        {
            using var stream = File.OpenRead(dependencyFilePath);
            document = JsonDocument.Parse(stream);
        }
        catch (Exception ex) when (ex is IOException || ex is JsonException || ex is UnauthorizedAccessException)
        {
            return assets;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return assets;
            }

            var libraryPaths = ReadLibraryPaths(root);
            if (!root.TryGetProperty("targets", out var targets) || targets.ValueKind != JsonValueKind.Object)
            {
                return assets;
            }

            foreach (var target in OrderedTargets(root, targets))
            {
                foreach (var library in target.EnumerateObject())
                {
                    if (library.Value.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }
                    if (!libraryPaths.TryGetValue(library.Name, out var libraryPath))
                    {
                        continue;
                    }
                    if (!library.Value.TryGetProperty("runtime", out var runtime) ||
                        runtime.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    foreach (var asset in runtime.EnumerateObject())
                    {
                        var simpleName = Path.GetFileNameWithoutExtension(asset.Name);
                        if (string.IsNullOrEmpty(simpleName) || assets.ContainsKey(simpleName))
                        {
                            continue;
                        }

                        assets[simpleName] = CombineRelative(libraryPath, asset.Name);
                    }
                }
            }
        }

        return assets;
    }

    private static Dictionary<string, string> ReadLibraryPaths(JsonElement root)
    {
        var libraryPaths = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!root.TryGetProperty("libraries", out var libraries) || libraries.ValueKind != JsonValueKind.Object)
        {
            return libraryPaths;
        }

        foreach (var library in libraries.EnumerateObject())
        {
            if (library.Value.ValueKind == JsonValueKind.Object &&
                library.Value.TryGetProperty("path", out var path) &&
                path.ValueKind == JsonValueKind.String)
            {
                var value = path.GetString();
                if (!string.IsNullOrEmpty(value))
                {
                    libraryPaths[library.Name] = value;
                }
            }
        }

        return libraryPaths;
    }

    /// <summary>
    /// The target the runtime would use first, then the rest. A deps file can
    /// carry more than one, and only one of them describes this run.
    /// </summary>
    private static IEnumerable<JsonElement> OrderedTargets(JsonElement root, JsonElement targets)
    {
        string? preferred = null;
        if (root.TryGetProperty("runtimeTarget", out var runtimeTarget) &&
            runtimeTarget.ValueKind == JsonValueKind.Object &&
            runtimeTarget.TryGetProperty("name", out var name) &&
            name.ValueKind == JsonValueKind.String)
        {
            preferred = name.GetString();
        }

        if (preferred != null &&
            targets.TryGetProperty(preferred, out var preferredTarget) &&
            preferredTarget.ValueKind == JsonValueKind.Object)
        {
            yield return preferredTarget;
        }

        foreach (var target in targets.EnumerateObject())
        {
            if (target.Name == preferred || target.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            yield return target.Value;
        }
    }

    /// <summary>
    /// Joins a library path and an asset path, both of which a deps file writes
    /// with forward slashes, into a path for this platform.
    /// </summary>
    private static string CombineRelative(string libraryPath, string assetPath) =>
        Path.Combine(
            libraryPath.Replace('/', Path.DirectorySeparatorChar),
            assetPath.Replace('/', Path.DirectorySeparatorChar));
}
