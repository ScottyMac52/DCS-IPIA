using Heimdall.Domain.MultiSim;

namespace Heimdall.App.Games
{
    /// <summary>Displays a cross-device native modifier without hiding its source identity.</summary>
    /// <param name="Modifier">Explicit captured source and key.</param>
    /// <param name="Name">Readable native device/key label.</param>
    internal sealed record EliteModifierChoice(Modifier Modifier, string Name)
    {
        #region BEGIN Public
        /// <summary>Gets the readable device/key chord entry.</summary>
        public override string ToString() => Name;
        #endregion END Public
    }
}
