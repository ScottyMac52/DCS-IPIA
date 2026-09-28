using System.Text.Json;
using Dcs.Ipia.Application;
namespace Dcs.Ipia.Dcs
{
    /// <summary>Serializes module data with deterministic ordering.</summary>
    public static class CanonicalSnapshotSerializer
    {
        private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
        #region BEGIN Public
        /// <summary>Creates canonical serialized content.</summary>
        /// <param name="values">The values.</param>
        /// <returns>The result of the operation.</returns>
        public static string Serialize(IEnumerable<ModuleDto> values)
        {
            ArgumentNullException.ThrowIfNull(values);
            return JsonSerializer
                       .Serialize(values.OrderBy(x => x.DcsId, StringComparer.Ordinal).ThenBy(x => x.Id), Options)
                       .Replace("\r\n", "\n", StringComparison.Ordinal) +
                   "\n";
        }
        /// <summary>Reads canonical serialized content.</summary>
        /// <param name="value">The value.</param>
        /// <returns>The result of the operation.</returns>
        public static IReadOnlyList<ModuleDto> Deserialize(string value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            return JsonSerializer.Deserialize<ModuleDto[]>(value, Options) ??
                   throw new JsonException("Snapshot did not contain modules.");
        }
        #endregion END Public
    }
}
