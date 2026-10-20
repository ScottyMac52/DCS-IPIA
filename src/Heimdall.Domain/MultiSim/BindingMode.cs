namespace Heimdall.Domain.MultiSim
{
    /// <summary>How a button binding fires.</summary>
    public enum BindingMode
    {
        #region BEGIN Public

        /// <summary>Fires on press.</summary>
        Press = 0,

        /// <summary>Fires while held.</summary>
        Hold = 1,

        /// <summary>Toggles on each press.</summary>
        Toggle = 2,

        /// <summary>Fires on double-tap.</summary>
        DoubleTap = 3,

        /// <summary>Fires on release.</summary>
        Release = 4

        #endregion END Public
    }
}
