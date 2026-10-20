namespace Dcs.Ipia.Domain.MultiSim
{
    /// <summary>A bindable command in a sim's command catalog.</summary>
    public sealed class SimCommand
    {
        #region BEGIN Public

        /// <summary>Gets or sets the internal command identifier.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Gets or sets the human-readable display name.</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Gets or sets the category.</summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>Gets or sets the command type.</summary>
        public CommandType Type { get; set; }

        /// <summary>Gets or sets a value indicating whether the command is module-specific.</summary>
        public bool IsModuleSpecific { get; set; }

        /// <summary>Gets or sets an optional context.</summary>
        public string? Context { get; set; }

        /// <summary>Gets or sets optional additional metadata.</summary>
        public Dictionary<string, string> Metadata { get; set; } = new();

        #endregion END Public
    }
}

