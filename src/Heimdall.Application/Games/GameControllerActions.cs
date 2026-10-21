namespace Heimdall.Application.Games
{
    /// <summary>Defines device-management availability independently of visual controls.</summary>
    /// <param name="Start">Start eligible controller identification.</param>
    /// <param name="Stop">Stop active identification, including during busy operations.</param>
    /// <param name="Confirm">Confirm a fresh compatible identification result.</param>
    /// <param name="Add">Add the selected unlinked connected controller.</param>
    /// <param name="Replace">Replace the selected physical reference.</param>
    /// <param name="Remove">Remove the selected physical reference.</param>
    /// <param name="Rescan">Refresh hardware discovery while idle.</param>
    /// <param name="Drawing">Change a linked controller's drawing.</param>
    public sealed record GameControllerActions(bool Start, bool Stop, bool Confirm, bool Add,
        bool Replace, bool Remove, bool Rescan, bool Drawing)
    {
        #region BEGIN Public
        /// <summary>Combines fresh action prerequisites with the shared busy and live-input states.</summary>
        public static GameControllerActions For(bool startReady, bool confirmReady, bool addReady,
            bool replaceReady, bool removeReady, bool drawingReady, bool identifying, bool testing, bool busy, bool devicesView)
        {
            var idle = devicesView && !identifying && !testing && !busy;
            return new(startReady && idle, identifying,
                confirmReady && identifying && devicesView && !testing && !busy,
                addReady && idle, replaceReady && idle, removeReady && idle, idle, drawingReady && idle);
        }
        #endregion END Public
    }
}
