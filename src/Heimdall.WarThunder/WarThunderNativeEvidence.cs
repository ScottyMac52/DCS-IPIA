namespace Heimdall.WarThunder
{
    /// <summary>Pins original catalog evidence independently of the current edited native source.</summary>
    internal sealed record WarThunderNativeEvidence(string ContextId, string FileName, string Text, string CatalogText);
}
