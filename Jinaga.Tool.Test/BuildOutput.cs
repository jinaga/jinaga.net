using System.Reflection;

namespace Jinaga.Tool.Test;

/// <summary>
/// The build output the tests read: the tool itself, and the rules assembly it
/// is run against. Both paths come from MSBuild through assembly metadata, so
/// neither is spelled out here, and a project moving breaks the build rather
/// than the tests.
/// </summary>
internal static class BuildOutput
{
    public static string ToolDirectory => Read("JinagaToolDirectory");

    public static string RulesLibraryDirectory => Read("RulesLibraryDirectory");

    public static string ToolPath => Path.Combine(ToolDirectory, "Jinaga.Tool.dll");

    public static string RulesAssemblyPath =>
        Path.Combine(RulesLibraryDirectory, "Jinaga.Tool.TestLibrary.dll");

    /// <summary>
    /// Copies the rules library's build output into a directory of its own, so
    /// a test can take something away from it without disturbing the build.
    /// </summary>
    public static string CopyRulesLibraryOutput(string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(RulesLibraryDirectory))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        }

        return Path.Combine(destination, Path.GetFileName(RulesAssemblyPath));
    }

    private static string Read(string key)
    {
        var value = typeof(BuildOutput).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(attribute => attribute.Key == key)
            ?.Value;

        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException(
                $"Jinaga.Tool.Test was built without the assembly metadata {key}.");
        }

        return Path.GetFullPath(value);
    }
}
