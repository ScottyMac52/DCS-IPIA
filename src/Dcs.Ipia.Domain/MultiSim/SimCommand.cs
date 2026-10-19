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

        /// <summary>Gets or sets a value indicating whether the command is aircraft-specific.</summary>
        public bool IsAircraftSpecific { get; set; }

        /// <summary>Gets or sets an optional context.</summary>
        public string? Context { get; set; }

        /// <summary>Gets or sets optional additional metadata.</summary>
        public Dictionary<string, string> Metadata { get; set; } = new();

        #endregion END Public
    }

    /// <summary>Kind of input a command accepts.</summary>
    public enum CommandType
    {
        /// <summary>Button or digital input.</summary>
        Button = 0,

        /// <summary>Analog axis.</summary>
        Axis = 1,

        /// <summary>Hat or POV.</summary>
        Hat = 2,

        /// <summary>Other or mixed.</summary>
        Other = 3
    }
}
