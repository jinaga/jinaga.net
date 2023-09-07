using System.Collections.Immutable;
using System.Reflection;

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
            else
            {
                arguments.ExpectEnd();
                Usage();
            }
        }
        catch (Exception ex)
        {
            WriteMessage(ex);
            Usage();
        }
    }

    private static async Task Deploy(CommandLineArguments arguments)
    {
        if (arguments.Consume("authorization"))
        {
            await DeployAuthorization(arguments);
        }
        else if (arguments.Consume("distribution"))
        {
            await DeployDistribution(arguments);
        }
        else
        {
            throw new ArgumentException("Expected deploy target authorization or distribution");
        }
    }

    private static async Task DeployAuthorization(CommandLineArguments arguments)
    {
        var assembly = arguments.Next();
        var endpoint = arguments.Next();
        var secret = arguments.Next();

        using var httpClient = new HttpClient();
        string authorization = GetAuthorizationFromAssembly(assembly);
        HttpContent content = new StringContent(authorization);
        content.Headers.Add("Authorization", $"Bearer {secret}");
        var result = await httpClient.PostAsync(endpoint, content);
        if (!result.IsSuccessStatusCode)
        {
            var message = await result.Content.ReadAsStringAsync();
            throw new ArgumentException($"Authorization deployment failed with status code {result.StatusCode}: {message}");
        }
    }

    private static Task DeployDistribution(CommandLineArguments arguments)
    {
        throw new NotImplementedException();
    }

    private static string GetAuthorizationFromAssembly(string path)
    {
        // Load the assembly
        var assembly = Assembly.LoadFrom(path);
        if (assembly == null)
        {
            throw new ArgumentException($"Could not load {path}");
        }

        // Find the type JinagaConfig
        var type = assembly.GetType("JinagaConfig");
        if (type == null)
        {
            throw new ArgumentException($"Expected type JinagaConfig in {path}");
        }

        // Find the method Authorization
        var method = type.GetMethod("Authorization", BindingFlags.Static | BindingFlags.Public);
        if (method == null)
        {
            throw new ArgumentException($"Expected method JinagaConfig.Authorization in {path}");
        }
        if (method.GetParameters().Length != 0)
        {
            throw new ArgumentException($"Expected method JinagaConfig.Authorization in {path} to have no parameters");
        }
        if (method.ReturnType != typeof(string))
        {
            throw new ArgumentException($"Expected method JinagaConfig.Authorization in {path} to return a string");
        }

        // Invoke the method
        var authorization = method.Invoke(null, new object[] { });
        if (authorization == null)
        {
            throw new ArgumentException($"JinagaConfig.Authorization in {path} returned null");
        }
        if (!(authorization is string))
        {
            throw new ArgumentException($"JinagaConfig.Authorization in {path} returned a non-string");
        }

        return (string)authorization;
    }

    private static void Usage()
    {
        Console.WriteLine("jinaga commands:");
        Console.WriteLine("  deploy authorization <assembly> <endpoint> <secret>");
        Console.WriteLine("  deploy distribution <assembly> <endpoint> <secret>");
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
