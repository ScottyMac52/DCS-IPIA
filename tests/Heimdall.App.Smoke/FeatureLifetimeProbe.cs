using System.Windows.Controls;
using Heimdall.Application.Interfaces;

namespace Heimdall.App.Smoke
{
    /// <summary>Checks ownership ordering independently of delayed WPF Loaded and Unloaded events.</summary>
    internal sealed class FeatureLifetimeProbe : UserControl, IGameWorkspaceFeatureLifetime
    {
        #region BEGIN Public
        internal bool Active { get; private set; }
        internal int Activations { get; private set; }
        internal int Releases { get; private set; }
        void IGameWorkspaceFeatureLifetime.SetFeatureActive(bool active)
        {
            Active = active;
            if (active) Activations++; else Releases++;
        }
        #endregion END Public
    }
}

