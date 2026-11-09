using Heimdall.Domain.MultiSim;

namespace Heimdall.Squadrons
{
    /// <summary>Plans command authoring against native evidence independently of controller enumeration.</summary>
    public static class SquadronsAssignmentPlanner
    {
        #region BEGIN Public
        /// <summary>Looks up the authoritative concept and supported captured records.</summary>
        public static SquadronsAssignmentPlan For(SquadronsProfileBackend backend, ProfileDocument profile, string concept)
        {
            ArgumentNullException.ThrowIfNull(backend);
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentException.ThrowIfNullOrWhiteSpace(concept);
            if (!backend.GetCatalog(profile).Commands.Any(x =>
                    x.Metadata.TryGetValue("squadrons.concept", out var value) && value == concept))
                throw new InvalidOperationException("Choose a concept present in the captured Squadrons command catalog.");
            return FromRecords(concept, backend.ReadBindings(profile).Count(x => x.Concept == concept), backend.ReadAssignableBindings(profile));
        }
        /// <summary>Builds the same native capability from cached assignable evidence, independently of raw-review decoding.</summary>
        public static SquadronsAssignmentPlan FromRecords(string concept, int capturedRecords, IEnumerable<SquadronsBindingRow> records)
        {
            var editable = records.Where(x => x.Concept == concept && x.Type == 3 && x.Fields.Count == 8 &&
                x.Fields["modifier"] == 0 && x.Fields["altbutton"] == 0 && x.Input is not null).Select(x => x.RecordId).ToArray();
            return new(concept, capturedRecords, editable.Length, editable);
        }
        #endregion END Public
    }
}
