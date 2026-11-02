namespace Jinaga.Tool.Test;

/// <summary>
/// A directory that exists for the length of one test.
/// </summary>
internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "jinaga-tool-test-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Combine(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, true);
        }
        catch (IOException)
        {
            // A leftover temporary directory is not worth failing a test over.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
