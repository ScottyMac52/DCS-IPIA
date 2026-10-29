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
            var records = backend.ReadAssignableBindings(profile).Where(x => x.Concept == concept).ToArray();
            var editable = records.Where(x => x.Type == 3 && x.Fields.Count == 8 &&
                x.Fields["modifier"] == 0 && x.Fields["altbutton"] == 0 &&
                x.Input is not null).Select(x => x.RecordId).ToArray();
            return new(concept, backend.ReadBindings(profile).Count(x => x.Concept == concept), editable.Length, editable);
        }
        #endregion END Public
    }
}
