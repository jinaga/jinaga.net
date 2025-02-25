using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace Jinaga.Notebooks;

internal static class ResourceLoader
{
    internal static string LoadVizJs()
    {
        return LoadResource("viz-standalone.js");
    }

    private static string LoadResource(string name)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"Jinaga.Notebooks.Resources.{name}";
        
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new InvalidOperationException($"Could not find embedded resource {resourceName}");
        }
        
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
