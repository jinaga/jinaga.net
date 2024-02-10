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
        else
        {
            throw new ArgumentException("Expected deploy target authorization or distribution");
        }
    }

    private static async Task<HttpClient> DeployRules(CommandLineArguments arguments, string methodName)
    {
        var assembly = arguments.Next();
        var endpoint = arguments.Next();
        var secret = arguments.Next();

        var httpClient = new HttpClient();
        string rules = GetRulesFromAssembly(assembly, methodName);
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
        else
        {
            throw new ArgumentException("Expected print target authorization or distribution");
        }
    }

    private static void PrintRules(CommandLineArguments arguments, string methodName)
    {
        var assembly = arguments.Next();
        var rules = GetRulesFromAssembly(assembly, methodName);
        Console.WriteLine(rules);
    }

    private static string GetRulesFromAssembly(string path, string methodName)
    {
        // Load the assembly
        var assembly = Assembly.LoadFrom(path);
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

        // Find the method Authorization
        var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public);
        if (method == null)
        {
            throw new ArgumentException($"Expected method JinagaConfig.{methodName} in {path}");
        }
        if (method.GetParameters().Length != 0)
        {
            throw new ArgumentException($"Expected method JinagaConfig.{methodName} in {path} to have no parameters");
        }
        if (method.ReturnType != typeof(string))
        {
            throw new ArgumentException($"Expected method JinagaConfig.{methodName} in {path} to return a string");
        }

        // Invoke the method
        var authorization = method.Invoke(null, new object[] { });
        if (authorization == null)
        {
            throw new ArgumentException($"JinagaConfig.{methodName} in {path} returned null");
        }
        if (!(authorization is string))
        {
            throw new ArgumentException($"JinagaConfig.{methodName} in {path} returned a non-string");
        }

        return (string)authorization;
    }

    private static void Usage()
    {
        Console.WriteLine("jinaga commands:");
        Console.WriteLine("  deploy authorization <assembly> <endpoint> <secret>");
        Console.WriteLine("  deploy distribution <assembly> <endpoint> <secret>");
        Console.WriteLine("  print authorization <assembly>");
        Console.WriteLine("  print distribution <assembly>");
        Console.WriteLine("");
        Console.WriteLine("The assembly should expose public static methods named");
        Console.WriteLine("JinagaConfig.Authorization and JinagaConfig.Distribution.");
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
