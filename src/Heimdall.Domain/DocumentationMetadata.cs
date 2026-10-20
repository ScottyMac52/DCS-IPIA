namespace Heimdall.Domain
{
    /// <summary>Represents documentation metadata.</summary>
    /// <param name="ModuleId">The module identifier.</param>
    /// <param name="OutputPath">The output path.</param>
    /// <param name="Title">The title.</param>
    public sealed record DocumentationMetadata(Guid ModuleId, string OutputPath, string Title);
}
