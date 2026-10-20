namespace Heimdall.Domain.MultiSim
{
    /// <summary>Response curve applied to an axis binding.</summary>
    public enum CurveType
    {
        #region BEGIN Public

        /// <summary>Linear response.</summary>
        Linear = 0,

        /// <summary>Exponential or power curve.</summary>
        Exponential = 1,

        /// <summary>S-curve.</summary>
        SCurve = 2,

        /// <summary>Sim-specific or custom curve.</summary>
        Custom = 3

        #endregion END Public
    }
}
