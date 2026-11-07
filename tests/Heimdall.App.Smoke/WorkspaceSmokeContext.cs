using System.Windows.Threading;
using Heimdall.App.Games;
using Heimdall.Application.Games;

namespace Heimdall.App.Smoke
{
    /// <summary>A non-game context for existing window and assignments base injection checks.</summary>
    internal sealed class WorkspaceSmokeContext(Func<bool> dirty, Func<string> fingerprint, Func<bool>? save = null)
        : GameWorkspaceContextBase<string>(new DispatcherWorkspaceScheduler(Dispatcher.CurrentDispatcher))
    {
        internal void Open() => ReplaceNativeSession(() => { });
        internal void NotifyNativeChanged() => NativeStateChanged();
        protected override NativeWorkspaceFacts ReadNativeFacts()
        {
            var yes = new WorkspaceAvailability(true, "");
            var write = new WorkspaceAvailability(save?.Invoke() != false, "Native save is unavailable.");
            return new("probe", true, false, "", dirty(), false, "", new(write, yes, yes, yes, yes, yes, yes));
        }
        protected override string ReadWorking() => fingerprint();
        protected override string ReadSaved() => "";
        protected override string CaptureNativeFingerprint() => fingerprint();
        protected override bool NameEquals(string left, string right) => left == right;
        protected override void DisposeNative() { }
    }
}
