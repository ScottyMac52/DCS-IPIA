using System.Text.Json.Serialization;

namespace Heimdall.Squadrons
{
    /// <summary>Pins source settings and context independently of changing connected hardware.</summary>
    /// <param name="ContextId">Captured profile context.</param>
    /// <param name="Text">Exact native source.</param>
    internal sealed record SquadronsNativeEvidence(string ContextId, string Text,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Templates = null);
}
