namespace Dcs.Ipia.Application
{
    /// <summary>Formats readable device labels independently of exact native DCS output identities.</summary>
    public static class DcsDeviceDisplay
    {
        #region BEGIN Public
        /// <summary>Removes a native GUID suffix and avoids repeating GUID-valued instance labels.</summary>
        /// <param name="nativeName">Device name or profile filename.</param>
        /// <param name="instance">Optional human role or physical instance.</param>
        /// <returns>A readable, distinguishable device label.</returns>
        public static string Label(string nativeName, string? instance = null)
        {
            var file = nativeName.EndsWith(".diff.lua", StringComparison.OrdinalIgnoreCase) ? nativeName : nativeName + ".diff.lua";
            var name = DcsPhysicalIdentity.FromFileName(file) is null
                ? file[..^9] : DcsPhysicalIdentity.NameFromFileName(file);
            if (string.IsNullOrWhiteSpace(instance)) return name;
            var unit = Guid.TryParse(instance, out var guid) ? "Unit " + guid.ToString("N")[..8] : instance.Trim();
            return name + " · " + unit;
        }
        #endregion END Public
    }
}
