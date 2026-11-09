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
        public void Post(Action action)
        {
#if PERFORMANCE_TRACE
            var queued = System.Diagnostics.Stopwatch.GetTimestamp();
            dispatcher.BeginInvoke(new Action(() =>
            {
                Heimdall.Application.PerformanceTrace.Record("dispatcher.queue-delay", System.Diagnostics.Stopwatch.GetElapsedTime(queued).TotalMilliseconds);
                using var performance = Heimdall.Application.PerformanceTrace.Measure("dispatcher.callback", GetType());
                action();
            }), DispatcherPriority.DataBind);
#else
            dispatcher.BeginInvoke(action, DispatcherPriority.DataBind);
#endif
        }
        #endregion END Public
    }
}
