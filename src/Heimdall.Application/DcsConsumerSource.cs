namespace Heimdall.Application
{
    /// <summary>Supplies one unchanged consumer repository file.</summary>
    /// <param name="Path">The repository-relative source path.</param>
    /// <param name="Content">The exact UTF-8 source text.</param>
    public sealed record DcsConsumerSource(string Path, string Content);
}
