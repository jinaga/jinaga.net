using System.Diagnostics;
using System.Text;

namespace Jinaga.Tool.Test;

/// <summary>
/// Runs the built tool the way a person does, in a process of its own. In
/// process it would prove nothing: this test assembly already has Jinaga and
/// its dependencies loaded, so every assembly the rules assembly needs would
/// resolve before the tool was asked to find it.
/// </summary>
internal static class ToolProcess
{
    public static string Run(string? packagesFolder, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        startInfo.ArgumentList.Add(BuildOutput.ToolPath);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (packagesFolder != null)
        {
            startInfo.Environment["NUGET_PACKAGES"] = packagesFolder;
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the tool.");

        var output = new StringBuilder();
        output.Append(process.StandardOutput.ReadToEnd());
        output.Append(process.StandardError.ReadToEnd());
        process.WaitForExit();

        return output.ToString();
    }
}
