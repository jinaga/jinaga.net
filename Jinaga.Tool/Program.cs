using System.Collections.Immutable;
using System.Reflection;
using System.Text;

namespace Jinaga.Tool;

internal class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            var arguments = new CommandLineArguments(args.ToImmutableArray());

            if (arguments.Consume("deploy"))
            {
                await Deploy(arguments);
            }
            else if (arguments.Consume("print"))
            {
                Print(arguments);
            }
            else
            {
                arguments.ExpectEnd();
                Usage();
            }
        }
        catch (Exception ex)
        {
            WriteMessage(ex);
        }
    }

    private static async Task Deploy(CommandLineArguments arguments)
    {
        if (arguments.Consume("authorization"))
        {
            await DeployRules(arguments, "Authorization");
        }
        else if (arguments.Consume("distribution"))
        {
            await DeployRules(arguments, "Distribution");
        }
        else if (arguments.Consume("purge"))
        {
            await DeployRules(arguments, "Purge");
        }
        else if (arguments.Consume("policy"))
        {
            var path = arguments.Next();
            var type = GetJinagaConfigType(path);
            var rules = GetPolicy(type, path);
            await DeployRulesToEndpoint(arguments, "Policy", rules);
        }
        else
        {
            throw new ArgumentException("Expected deploy target authorization, distribution, or purge");
        }
    }

    private static async Task<HttpClient> DeployRules(CommandLineArguments arguments, string methodName)
    {
        var path = arguments.Next();
        string rules = GetRulesFromAssembly(path, methodName);
        return await DeployRulesToEndpoint(arguments, methodName, rules);
    }

    private static async Task<HttpClient> DeployRulesToEndpoint(CommandLineArguments arguments, string methodName, string rules)
    {
        var endpoint = arguments.Next();
        var secret = arguments.Next();

        var httpClient = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Add("Authorization", $"Bearer {secret}");
        var content = new StringContent(rules, Encoding.UTF8, "text/plain");
        request.Content = content;
        var result = await httpClient.SendAsync(request);
        if (!result.IsSuccessStatusCode)
        {
            var message = await result.Content.ReadAsStringAsync();
            throw new ArgumentException($"{methodName} deployment failed with status code {result.StatusCode}: {message}");
        }

        Console.WriteLine($"{methodName} deployed");
        return httpClient;
    }

    private static void Print(CommandLineArguments arguments)
    {
        if (arguments.Consume("authorization"))
        {
            PrintRules(arguments, "Authorization");
        }
        else if (arguments.Consume("distribution"))
        {
            PrintRules(arguments, "Distribution");
        }
        else if (arguments.Consume("purge"))
        {
            PrintRules(arguments, "Purge");
        }
        else if (arguments.Consume("policy"))
        {
            var path = arguments.Next();
            var type = GetJinagaConfigType(path);
            var rules = GetPolicy(type, path);
            Console.WriteLine(rules);
        }
        else
        {
            throw new ArgumentException("Expected print target authorization, distribution, purge, or policy");
        }
    }

    private static void PrintRules(CommandLineArguments arguments, string methodName)
    {
        var path = arguments.Next();
        var rules = GetRulesFromAssembly(path, methodName);
        Console.WriteLine(rules);
    }

    private static string GetRulesFromAssembly(string path, string methodName)
    {
        var type = GetJinagaConfigType(path);
        return GetRules(type, methodName, path);
    }

    /// <summary>
    /// Finds the rules method that describes a policy: the one that takes no
    /// parameters. Selecting it by signature rather than by name alone is what
    /// lets a JinagaConfig name its builder after the description it produces,
    /// which is the natural shape because a test passes the builder to
    /// JinagaTestOptions while the application passes the description here.
    /// Looking up by name alone throws AmbiguousMatchException on exactly that
    /// class.
    /// </summary>
    /// <returns>The parameterless overload, or null when the type declares none.</returns>
    internal static MethodInfo? FindRulesMethod(Type type, string methodName) =>
        type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public, null, Type.EmptyTypes, null);

    /// <summary>
    /// Whether the type declares any public static method of this name, with
    /// or without parameters. Distinguishes a JinagaConfig that forgot the
    /// description method from one that named it but gave it parameters, which
    /// are different mistakes and get different messages.
    /// </summary>
    private static bool DeclaresRulesMethod(Type type, string methodName) =>
        type.GetMethods(BindingFlags.Static | BindingFlags.Public).Any(m => m.Name == methodName);

    /// <summary>
    /// Invokes the parameterless rules method and returns the description it
    /// produces. The one path print, deploy and policy all read rules through,
    /// so they cannot disagree about which overload describes a policy.
    /// </summary>
    internal static string GetRules(Type type, string methodName, string path)
    {
        var method = FindRulesMethod(type, methodName);
        if (method == null)
        {
            if (DeclaresRulesMethod(type, methodName))
            {
                throw new ArgumentException($"Expected method JinagaConfig.{methodName} in {path} to have no parameters");
            }
            throw new ArgumentException($"Expected method JinagaConfig.{methodName} in {path}");
        }
        if (method.ReturnType != typeof(string))
        {
            throw new ArgumentException($"Expected method JinagaConfig.{methodName} in {path} to return a string");
        }

        // Invoke the method
        var rules = method.Invoke(null, []);
        if (rules == null)
        {
            throw new ArgumentException($"JinagaConfig.{methodName} in {path} returned null");
        }
        if (!(rules is string))
        {
            throw new ArgumentException($"JinagaConfig.{methodName} in {path} returned a non-string");
        }

        return (string)rules;
    }

    private static Type GetJinagaConfigType(string path)
    {
        // The tool ships no copy of Jinaga, so the rules assembly's own
        // dependencies have to be resolved before anything in it is invoked.
        // Register before the load and leave the hook in place: a JinagaConfig
        // method body is what pulls Jinaga in, and that runs later.
        var fullPath = Path.GetFullPath(path);
        RulesAssemblyDependencyResolver.Register(fullPath);

        // Load the assembly
        var assembly = Assembly.LoadFrom(fullPath);
        if (assembly == null)
        {
            throw new ArgumentException($"Could not load {path}");
        }

        // Get all public classes
        var types = assembly.GetTypes();
        var publicClasses = types.Where(t => t.IsPublic);

        // Find the type JinagaConfig
        var configTypes = publicClasses.Where(t => t.Name == "JinagaConfig");
        if (!configTypes.Any())
        {
            throw new ArgumentException($"Expected type JinagaConfig in {path}");
        }
        if (configTypes.Count() > 1)
        {
            throw new ArgumentException($"Expected only one type JinagaConfig in {path}");
        }
        var type = configTypes.Single();
        return type;
    }

    internal static string GetPolicy(Type type, string path)
    {
        var authorization = GetOptionalRules(type, "Authorization", path);
        var distribution = GetOptionalRules(type, "Distribution", path);
        var purge = GetOptionalRules(type, "Purge", path);

        return $"{authorization}\n{distribution}\n{purge}".Trim();
    }

    /// <summary>
    /// Reads one of the three rules for the policy command, where a
    /// JinagaConfig is allowed to leave a rule out. A type that does not
    /// declare the method at all contributes nothing; one that declares it
    /// must satisfy the same contract the rules commands demand, so a policy
    /// never silently omits a rule the author did write.
    /// </summary>
    private static string GetOptionalRules(Type type, string methodName, string path) =>
        FindRulesMethod(type, methodName) == null && !DeclaresRulesMethod(type, methodName)
            ? string.Empty
            : GetRules(type, methodName, path);

    private static void Usage()
    {
        Console.WriteLine("jinaga commands:");
        Console.WriteLine("  deploy authorization <assembly> <endpoint> <secret>");
        Console.WriteLine("  deploy distribution <assembly> <endpoint> <secret>");
        Console.WriteLine("  deploy purge <assembly> <endpoint> <secret>");
        Console.WriteLine("  deploy policy <assembly> <endpoint> <secret>");
        Console.WriteLine("  print authorization <assembly>");
        Console.WriteLine("  print distribution <assembly>");
        Console.WriteLine("  print purge <assembly>");
        Console.WriteLine("  print policy <assembly>");
        Console.WriteLine("");
        Console.WriteLine("The assembly should expose public static methods named");
        Console.WriteLine("JinagaConfig.Authorization, JinagaConfig.Distribution, and JinagaConfig.Purge.");
    }

    private static void WriteMessage(Exception ex)
    {
        Exception? visit = ex;
        while (visit != null)
        {
            Console.WriteLine(visit.Message);
            visit = visit.InnerException;
        }
    }
}
