namespace Dcs.Ipia.Application
{
    /// <summary>A reviewed addition or metadata update to the aircraft command catalog.</summary>
    /// <param name="Current">Existing command, or null for an addition.</param>
    /// <param name="Incoming">Imported command with the same stable DCS code.</param>
    public sealed record CommandCatalogImportChange(CommandPaletteEntry? Current, CommandPaletteEntry Incoming)
    {
        #region BEGIN Public
        /// <summary>Gets the merge action.</summary>
        public string Action => Current is null ? "Add" : Current == Incoming ? "Unchanged" : "Update";
        /// <summary>Gets the stable DCS code.</summary>
        public string Code => Incoming.Id;
        /// <summary>Gets the previous name.</summary>
        public string OldName => Current?.Name ?? "";
        /// <summary>Gets the proposed name.</summary>
        public string Name => Incoming.Name;
        /// <summary>Gets the previous category.</summary>
        public string OldCategory => Current?.Category ?? "";
        /// <summary>Gets the proposed category.</summary>
        public string Category => Incoming.Category;
        /// <summary>Gets the previous input type.</summary>
        public string OldType => Current is null ? "" : Current.IsAxis ? "Axis" : "Key";
        /// <summary>Gets the proposed input type.</summary>
        public string Type => Incoming.IsAxis ? "Axis" : "Key";
        #endregion END Public
    }
}
