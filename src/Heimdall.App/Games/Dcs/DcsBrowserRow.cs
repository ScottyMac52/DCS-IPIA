using Heimdall.Application;
using Heimdall.Dcs;

namespace Heimdall.App.Games.Dcs
{
    /// <summary>Typed DCS browser row retaining its native palette command and bound count.</summary>
    internal sealed record DcsBrowserRow(DcsCommandPaletteEntry Command, int Bound)
    {
        #region BEGIN Public
        /// <summary>Gets the native command identity.</summary>
        public string Id => Command.Id;
        /// <summary>Gets the native command name.</summary>
        public string Name => Command.Name;
        /// <summary>Gets the native category.</summary>
        public string Category => Command.Category;
        /// <summary>Gets whether the native command is an axis.</summary>
        public bool IsAxis => Command.IsAxis;
        #endregion END Public
    }
}
