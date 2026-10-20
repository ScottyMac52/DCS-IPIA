namespace Dcs.Ipia.EliteDangerous
{
    /// <summary>Versioned original text; baseline projections are always rebuilt from this evidence.</summary>
    internal sealed record EliteNativeEvidence(string ContextId, Dictionary<string, string> Sources);
}
