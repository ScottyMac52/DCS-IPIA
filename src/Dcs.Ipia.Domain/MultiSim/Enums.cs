namespace Dcs.Ipia.Domain.MultiSim
{
    /// <summary>Response curve applied to an axis binding.</summary>
    public enum CurveType
    {
        /// <summary>Linear response.</summary>
        Linear = 0,

        /// <summary>Exponential or power curve.</summary>
        Exponential = 1,

        /// <summary>S-curve.</summary>
        SCurve = 2,

        /// <summary>Sim-specific or custom curve.</summary>
        Custom = 3
    }

    /// <summary>How a button binding fires.</summary>
    public enum BindingMode
    {
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
    }
}
