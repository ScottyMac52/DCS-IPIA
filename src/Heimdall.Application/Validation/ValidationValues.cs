using System.Globalization;
using System.Text.Json;

namespace Heimdall.Application.Validation
{
    /// <summary>Canonical scalar identities and bounded display text for independent rule comparisons.</summary>
    internal static class ValidationValues
    {
        #region BEGIN Public
        internal static string Key(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.String => "string:" + value.GetString(),
            JsonValueKind.Number => "number:" + (value.TryGetDecimal(out var number) ? number.ToString("G29", CultureInfo.InvariantCulture) : value.GetDouble().ToString("R", CultureInfo.InvariantCulture)),
            _ => value.ValueKind + ":" + Display(value)
        };
        internal static string Display(JsonElement value)
        {
            var text = value.ValueKind == JsonValueKind.Undefined ? "<missing>" : value.GetRawText();
            return text.Length <= 256 ? text : text[..256] + "…";
        }
        #endregion END Public
    }
}
