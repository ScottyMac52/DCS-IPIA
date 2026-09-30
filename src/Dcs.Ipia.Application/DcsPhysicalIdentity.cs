namespace Dcs.Ipia.Application
{
    /// <summary>Reads physical DirectInput identity from native DCS filenames without inventing GUIDs.</summary>
    public static class DcsPhysicalIdentity
    {
        #region BEGIN Public
        /// <summary>Reads the final GUID-qualified filename suffix.</summary>
        /// <param name="fileName">Native DCS profile filename.</param>
        /// <returns>The physical GUID, or null for an unqualified or malformed filename.</returns>
        public static Guid? FromFileName(string fileName)
        {
            if (!fileName.EndsWith("}.diff.lua", StringComparison.OrdinalIgnoreCase)) return null;
            var start = fileName.LastIndexOf(" {", StringComparison.Ordinal);
            if (start <= 0) return null;
            var value = fileName.AsSpan(start + 2, fileName.Length - start - 12);
            return Guid.TryParseExact(value, "D", out var guid) && guid != Guid.Empty ? guid : null;
        }
        /// <summary>Reads a native controller name without its GUID and file extension.</summary>
        /// <param name="fileName">GUID-qualified filename.</param>
        /// <returns>Native controller name.</returns>
        public static string NameFromFileName(string fileName) => FromFileName(fileName) is null
            ? throw new ArgumentException("A GUID-qualified DCS filename is required.", nameof(fileName))
            : fileName[..fileName.LastIndexOf(" {", StringComparison.Ordinal)];
        /// <summary>Merges attached devices and captured profiles without collapsing repeated physical units.</summary>
        /// <param name="connected">Attached controllers.</param>
        /// <param name="profiles">Captured profile identities.</param>
        /// <returns>Stable choices with captured DCS names preferred and connection status preserved.</returns>
        public static IReadOnlyList<DetectedDcsDevice> Merge(IEnumerable<DetectedDcsDevice> connected,
                                                              IEnumerable<DetectedDcsDevice> profiles)
        {
            return connected.Concat(profiles).Where(x => x.PhysicalGuid != Guid.Empty && !string.IsNullOrWhiteSpace(x.Name))
                .GroupBy(x => x.PhysicalGuid).Select(g => {
                    var captured = g.Where(x => x.CapturedFileName is not null).OrderBy(x => x.CapturedFileName, StringComparer.Ordinal).FirstOrDefault();
                    var chosen = captured ?? g.OrderBy(x => x.Name, StringComparer.Ordinal).First();
                    return chosen with { Connected = g.Any(x => x.Connected) };
                }).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.PhysicalGuid).ToArray();
        }
        #endregion END Public
    }
}
