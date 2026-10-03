using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;

namespace DcsIpia.Presentation
{
    /// <summary>A profile row whose suggested hardware must be reviewed before module import.</summary>
    public sealed class SavedGamesProfileChoice
    {
        /// <summary>Creates a hardware selection row for one captured profile.</summary>
        /// <param name="profile">Captured physical filename and source identity.</param>
        /// <param name="bindings">Bindings belonging to this profile.</param>
        /// <param name="choices">Pinned hardware choices.</param>
        /// <param name="suggested">Recognition-rule suggestion, or null for manual selection.</param>
        public SavedGamesProfileChoice(DeviceProfile profile, IReadOnlyList<DcsBinding> bindings,
            IReadOnlyList<WorkspaceHardware> choices, WorkspaceHardware? suggested)
        {
            Profile = profile;
            HardwareChoices = choices;
            Hardware = suggested;
            Summary = bindings.Count(x => x.Disposition != BindingDisposition.Removed) + " assignments · " +
                bindings.Count(x => x.Section == BindingSection.Axis && x.Disposition == BindingDisposition.Removed) + " axis removals";
        }
        #region BEGIN Public
        /// <summary>Gets the source profile whose bindings will be preserved.</summary>
        public DeviceProfile Profile { get; }
        /// <summary>Gets the captured native filename, including physical instance identity.</summary>
        public string FileName => Profile.SourceName;
        /// <summary>Gets assignment and axis-removal counts, including zero for empty profiles.</summary>
        public string Summary { get; }
        /// <summary>Gets the internal hardware catalog available for manual correction.</summary>
        public IReadOnlyList<WorkspaceHardware> HardwareChoices { get; }
        /// <summary>Gets or sets the reviewed hardware association; null blocks import.</summary>
        public WorkspaceHardware? Hardware { get; set; }
        #endregion END Public
    }
}
