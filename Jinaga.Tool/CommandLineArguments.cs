using System.Collections.Immutable;

namespace Jinaga.Tool;

internal class CommandLineArguments
{
    private ImmutableArray<string> args;

    public CommandLineArguments(ImmutableArray<string> args)
    {
        this.args = args;
    }

    public bool Continues(string expected)
    {
        return args.Any() && args[0] == expected;
    }

    public bool Consume(string expected)
    {
        if (Continues(expected))
        {
            args = args.RemoveAt(0);
            return true;
        }
        return false;
    }

    public string Next()
    {
        if (!args.Any())
        {
            throw new ArgumentException("Expecting more arguments");
        }
        var arg = args[0];
        args = args.RemoveAt(0);
        return arg;
    }

    public void ExpectEnd()
    {
        if (args.Any())
        {
            throw new ArgumentException($"Unexpected arguments {string.Join(" ", args)}");
        }
    }
}