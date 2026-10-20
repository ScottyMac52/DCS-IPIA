using Heimdall.Domain;

namespace Heimdall.Application
{
    /// <summary>Contains a read-only view of DCS Saved Games joystick profile files.</summary>
    public sealed class DcsFolderImportResult
    {
        /// <summary>Creates a folder import result.</summary>
        /// <param name="folder">The selected joystick folder.</param>
        /// <param name="profiles">Profiles identified by their source filenames.</param>
        /// <param name="bindings">Parsed aircraft bindings.</param>
        /// <param name="modifiers">Parsed adjacent modifiers, when available.</param>
        /// <param name="diagnostics">Import diagnostics.</param>
        public DcsFolderImportResult(string folder, IReadOnlyList<DeviceProfile> profiles,
                                     IReadOnlyList<DcsBinding> bindings, IReadOnlyList<DcsModifier> modifiers,
                                     IReadOnlyList<ImportDiagnostic> diagnostics)
        {
            Folder = folder;
            Profiles = profiles;
            Bindings = bindings;
            Modifiers = modifiers;
            Diagnostics = diagnostics;
        }
        #region BEGIN Public
        /// <summary>Gets the selected local directory.</summary>
        public string Folder { get; }
        /// <summary>Gets profile names without assuming a shared device identity.</summary>
        public IReadOnlyList<DeviceProfile> Profiles { get; }
        /// <summary>Gets the aircraft bindings.</summary>
        public IReadOnlyList<DcsBinding> Bindings { get; }
        /// <summary>Gets the adjacent DCS modifiers.</summary>
        public IReadOnlyList<DcsModifier> Modifiers { get; }
        /// <summary>Gets path-specific diagnostics.</summary>
        public IReadOnlyList<ImportDiagnostic> Diagnostics { get; }
        /// <summary>Gets frozen relative profile and modifier source text for portable recovery.</summary>
        public IReadOnlyDictionary<string, string> Sources { get; init; } = new Dictionary<string, string>();
        /// <summary>Gets whether at least one profile was imported without an error.</summary>
        public bool Succeeded => Profiles.Count > 0 && !Diagnostics.Any(x => x.IsError);
        #endregion END Public
    }
}
