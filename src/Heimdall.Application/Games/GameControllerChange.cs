namespace Heimdall.Application.Games
{
    /// <summary>An immutable review handle bound to one session revision and optional live replacement capabilities.</summary>
    public sealed class GameControllerChange
    {
        internal GameControllerChange(GameProfileSession owner, string baseline, GameWorkspace candidate, string summary,
            string? resultDeviceId, DetectedDcsDevice? target = null, Func<IReadOnlyCollection<string>>? inputs = null, IReadOnlyCollection<string>? keys = null)
        {
            Owner = owner; Baseline = baseline; Candidate = candidate; Summary = summary; ResultDeviceId = resultDeviceId;
            Target = target; Inputs = inputs; Keys = keys?.ToHashSet(StringComparer.Ordinal) ?? [];
        }
        internal GameProfileSession Owner { get; }
        internal string Baseline { get; }
        internal GameWorkspace Candidate { get; }
        internal DetectedDcsDevice? Target { get; }
        internal Func<IReadOnlyCollection<string>>? Inputs { get; }
        internal HashSet<string> Keys { get; }
        #region BEGIN Public
        /// <summary>Gets the complete binding impact to review before staging changes.</summary>
        public string Summary { get; }
        /// <summary>Gets the controller to select after replacement, or null after removal.</summary>
        public string? ResultDeviceId { get; }
        #endregion END Public
    }
}
