namespace Heimdall.Domain.MultiSim
{
    /// <summary>Kind of input a command accepts.</summary>
    public enum CommandType
    {
        #region BEGIN Public

        /// <summary>Button or digital input.</summary>
        Button = 0,

        /// <summary>Analog axis.</summary>
        Axis = 1,

        /// <summary>Hat or POV.</summary>
        Hat = 2,

        /// <summary>Other or mixed.</summary>
        Other = 3

        #endregion END Public
    }
}
