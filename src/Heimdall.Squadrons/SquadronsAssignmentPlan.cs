using Heimdall.Domain.MultiSim;

namespace Heimdall.Squadrons
{
    /// <summary>Adapter-owned capability for command-first assignment; no UI or native index guesses.</summary>
    public sealed record SquadronsAssignmentPlan(string Concept, int CapturedRecords, int EditableRecords,
        IReadOnlyList<string> EditableRecordIds)
    {
        /// <summary>Existing safe edit slots can be assigned through the established native record writer.</summary>
        public bool CanReplaceCapturedRecord => EditableRecords > 0;
        /// <summary>New native records must not be fabricated until their index/identifier rules are verified.</summary>
        public bool RequiresNewNativeRecord => EditableRecords == 0;
    }

    /// <summary>Plans command authoring against native evidence independently of controller enumeration.</summary>
    public static class SquadronsAssignmentPlanner
    {
        /// <summary>Looks up the authoritative concept and supported captured records.</summary>
        public static SquadronsAssignmentPlan For(SquadronsProfileBackend backend, ProfileDocument profile, string concept)
        {
            ArgumentNullException.ThrowIfNull(backend);
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentException.ThrowIfNullOrWhiteSpace(concept);
            if (!backend.GetCatalog(profile).Commands.Any(x =>
                    x.Metadata.TryGetValue("squadrons.concept", out var value) && value == concept))
                throw new InvalidOperationException("Choose a concept present in the captured Squadrons command catalog.");
            var records = backend.ReadBindings(profile).Where(x => x.Concept == concept).ToArray();
            var editable = records.Where(x => x.Type == 3 && x.Fields.Count == 8 &&
                x.Fields["modifier"] == 0 && x.Fields["altbutton"] == 0 &&
                x.Input is not null).Select(x => x.RecordId).ToArray();
            return new(concept, records.Length, editable.Length, editable);
        }
    }
}
