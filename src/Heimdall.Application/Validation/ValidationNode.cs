using System.Text.Json;

namespace Heimdall.Application.Validation
{
    /// <summary>One selected JSON value, retaining the concrete path after wildcard expansion.</summary>
    internal sealed record ValidationNode(string Path, JsonElement Value);
}
