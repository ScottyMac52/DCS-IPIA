namespace Heimdall.Application
{
    /// <summary>Bridges existing authored DCS-style drawing keys to reported object IDs; never determines native game slot counts.</summary>
    public static class ControllerDrawingAliases
    {
        #region BEGIN Public
        /// <summary>Projects only compatibility-supported aliases while retaining the complete reported roster separately.</summary>
        public static IReadOnlyDictionary<string, string> Create(ControllerInputCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            if (catalog.SchemaVersion != 1) throw new InvalidDataException("Unsupported reported input catalog version.");
            var compatibility = ControllerCapabilityMapper.Map(catalog.Objects.Select(input => input.Evidence));
            return compatibility.Inputs.ToDictionary(input => ControllerInputCatalog.Identify(input.Evidence), input => input.DcsKey, StringComparer.Ordinal);
        }
        #endregion END Public
    }
}
