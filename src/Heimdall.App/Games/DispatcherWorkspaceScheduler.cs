using System.Windows.Threading;
using Heimdall.Application.Interfaces;

namespace Heimdall.App.Games
{
    /// <summary>Publishes workspace transitions on the owning WPF dispatcher without recursive callbacks.</summary>
    internal sealed class DispatcherWorkspaceScheduler(Dispatcher dispatcher) : IWorkspaceScheduler
    {
        #region BEGIN Public
        /// <inheritdoc/>
        public bool CheckAccess() => dispatcher.CheckAccess();
        /// <inheritdoc/>
        public void VerifyAccess() => dispatcher.VerifyAccess();
        /// <inheritdoc/>
        public void Post(Action action) => dispatcher.BeginInvoke(action, DispatcherPriority.DataBind);
        #endregion END Public
    }
}
