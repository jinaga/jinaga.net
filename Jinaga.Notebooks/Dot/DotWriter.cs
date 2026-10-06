namespace Jinaga.Notebooks.Dot;

/// <summary>
/// Spelling rules shared by the DOT writers. A fact type name is whatever its
/// attribute says, so a name that contains a quote or a backslash has to be
/// escaped rather than written through.
/// </summary>
static class DotWriter
{
    public static string Quote(string name)
    {
        return "\"" + name.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
