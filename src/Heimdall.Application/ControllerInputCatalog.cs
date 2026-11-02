using System.Globalization;

namespace Heimdall.Application
{
    /// <summary>Complete native object roster; no recognized-key filter, control limit or sorting can discard physical evidence.</summary>
    public sealed record ControllerInputCatalog(int SchemaVersion, IReadOnlyList<PhysicalControllerInput> Objects, IReadOnlyList<string> Diagnostics)
    {
        #region BEGIN Public
        /// <summary>Gets whether every object has metadata from the current native discovery boundary.</summary>
        public bool HasNativeDetails => Objects.Count > 0 && Objects.All(input => input.Evidence.Details is not null);
        /// <summary>Builds simulator-neutral identities without converting raw offsets into game indices.</summary>
        public static ControllerInputCatalog Create(IReadOnlyList<ControllerObject>? evidence)
        {
            if (evidence is null) return new(1, [], ["Native object evidence is absent. Rescan the controller."]);
            var identities = evidence.Select(Identity).ToArray();
            var duplicates = identities.GroupBy(id => id, StringComparer.Ordinal).Where(group => group.Count() > 1)
                .Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
            var inputs = evidence.Select((item, index) => new PhysicalControllerInput(identities[index], index, Kind(item.NativeType),
                item, duplicates.Contains(identities[index]))).ToArray();
            var diagnostics = duplicates.Select(id => "Duplicate native object identity: " + id).ToList();
            if (evidence.Count == 0) diagnostics.Add("The controller returned no native objects.");
            if (evidence.Any(item => item.Details is null)) diagnostics.Add("Legacy object evidence lacks native metadata. Rescan before native layout translation.");
            return new(1, inputs, diagnostics);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static string Identity(ControllerObject item) => "directinput:" + item.ObjectGuid.ToString("N") + ":"
            + (item.NativeType & 0x00ffffff).ToString("x6", CultureInfo.InvariantCulture);
        private static PhysicalInputKind Kind(uint type)
        {
            if ((type & 3) != 0) return PhysicalInputKind.Axis;
            if ((type & 12) != 0) return PhysicalInputKind.Button;
            return (type & 16) != 0 ? PhysicalInputKind.Hat : PhysicalInputKind.Other;
        }
        #endregion END private helpers
    }
}
