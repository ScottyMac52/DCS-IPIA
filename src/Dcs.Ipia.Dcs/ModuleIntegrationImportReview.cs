using Dcs.Ipia.Application;

namespace Dcs.Ipia.Dcs
{
    /// <summary>Immutable proposal for importing module instructions without changing any live state.</summary>
    /// <param name="BaselineSha256">Reviewed complete editor digest.</param>
    /// <param name="FileName">Original safe basename.</param>
    /// <param name="Content">Original source text.</param>
    /// <param name="Instructions">Validated proposed instruction groups.</param>
    public sealed record ModuleIntegrationImportReview(string BaselineSha256, string FileName, string Content,
        IReadOnlyList<ModuleIntegrationInstruction> Instructions);
}
