using System.Globalization;

namespace Heimdall.Application
{
    /// <summary>Complete native object roster; declarations are retained independently of authored hardware controls and live verification.</summary>
    public sealed record ControllerInputCatalog(int SchemaVersion, IReadOnlyList<ReportedControllerInput> Objects, IReadOnlyList<string> Diagnostics)
    {
        #region BEGIN Public
        /// <summary>Gets counts of driver-declared objects, without claiming that each is a physical or reachable control.</summary>
        public ReportedInputCounts Counts => new(Objects.Count(input => input.Kind == ReportedInputKind.Axis),
            Objects.Count(input => input.Kind == ReportedInputKind.Button), Objects.Count(input => input.Kind == ReportedInputKind.Hat),
            Objects.Count(input => input.Kind == ReportedInputKind.Other));
        /// <summary>Gets whether every object has metadata from the current native discovery boundary.</summary>
        public bool HasNativeDetails => Objects.Count > 0 && Objects.All(input => input.Evidence.Details is not null);
        /// <summary>Builds simulator-neutral identities without converting raw offsets into game indices.</summary>
        public static ControllerInputCatalog Create(IReadOnlyList<ControllerObject>? evidence)
        {
            if (evidence is null) return new(1, [], ["Native object evidence is absent. Rescan the controller."]);
            var identities = evidence.Select(Identify).ToArray();
            var duplicates = identities.GroupBy(id => id, StringComparer.Ordinal).Where(group => group.Count() > 1)
                .Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
            var inputs = evidence.Select((item, index) => new ReportedControllerInput(identities[index], index, Kind(item.NativeType),
                item, duplicates.Contains(identities[index]))).ToArray();
            var diagnostics = duplicates.Select(id => "Duplicate native object identity: " + id).ToList();
            if (evidence.Count == 0) diagnostics.Add("The controller returned no native objects.");
            if (evidence.Any(item => item.Details is null)) diagnostics.Add("Legacy object evidence lacks native metadata. Rescan before native layout translation.");
            return new(1, inputs, diagnostics);
        }
        /// <summary>Gets the stable local identity of a driver-declared object, independent of labels and layout order.</summary>
        public static string Identify(ControllerObject item) => "directinput:" + item.ObjectGuid.ToString("N") + ":"
            + (item.NativeType & 0x00ffffff).ToString("x6", CultureInfo.InvariantCulture);
        #endregion END Public
        #region BEGIN private helpers
        private static ReportedInputKind Kind(uint type)
        {
            if ((type & 3) != 0) return ReportedInputKind.Axis;
            if ((type & 12) != 0) return ReportedInputKind.Button;
            return (type & 16) != 0 ? ReportedInputKind.Hat : ReportedInputKind.Other;
        }
        #endregion END private helpers
    }
}
