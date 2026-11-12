using Heimdall.Application.Games;

namespace Heimdall.Application.Interfaces
{
    /// <summary>Shares workspace state, edit operations and service boundaries among feature views.</summary>
    public interface IDcsWorkspaceContext : IGameWorkspaceContext<DcsEditorWorkspaceSnapshot>
    {
        #region BEGIN Public
        /// <summary>Gets the current personal assignment labels keyed by slot identity.</summary>
        IReadOnlyDictionary<string, string> Labels { get; }
        /// <summary>Sets or clears a personal assignment label and refreshes workspace state.</summary>
        void SetLabel(string key, string? value);
        /// <summary>Stages a reviewed batch of personal labels with one workspace notification.</summary>
        void SetLabels(IReadOnlyList<KeyValuePair<string, string?>> labels);
        /// <summary>Gets the assignment draft service for pending edits.</summary>
        DcsDraftAssignmentService? Draft { get; }

        /// <summary>Gets the optional saved workspace repository.</summary>
        IDcsEditorWorkspaceRepository? Repository { get; }
        /// <summary>Gets the connected-device discovery boundary.</summary>
        IConnectedDeviceDiscovery DeviceDiscovery { get; }
        /// <summary>Gets the optional adopted hardware inventory manager.</summary>
        HardwareInventoryManager? Inventory { get; }
        /// <summary>Gets the optional immutable hardware drawing library.</summary>
        IUserMappingLibrary? UserMappings { get; }
        /// <summary>Gets an optional adapter for confirming a reviewed drawing change.</summary>
        Func<DcsMappingRevisionReview, bool>? MappingReviewConfirmation { get; }
        /// <summary>Gets an optional adapter for selecting command catalog files.</summary>
        Func<bool, IReadOnlyList<string>?>? CommandFilePicker { get; }
        /// <summary>Gets an optional adapter for selecting integration instructions.</summary>
        Func<string?>? IntegrationFilePicker { get; }
        /// <summary>Gets an optional factory for opening live controller input sources.</summary>
        Func<nint, IReadOnlyList<Guid>, IDeviceInputSource>? InputSourceFactory { get; }
        /// <summary>Gets an optional adapter for publishing input log HTML and text to the clipboard.</summary>
        Action<string, string>? InputLogClipboardWriter { get; }
        /// <summary>Gets an optional adapter for selecting a DCS axis defaults profile.</summary>
        Func<string?>? AxisDefaultsFilePicker { get; }
        /// <summary>Gets or sets the current Saved Games source directory.</summary>
        string SavedGamesRoot { get; set; }


        /// <summary>Stages a structural workspace edit using the shared operation gate, preserving native rejection reasons.</summary>
        WorkspaceOperationResult RunStructureEdit(Func<DcsEditorWorkspaceSnapshot, DcsEditorWorkspaceSnapshot> edit);
        /// <summary>Saves the current workspace draft to its repository.</summary>
        Task SaveAsync();
        /// <summary>Discards pending workspace edits.</summary>
        void Discard();
        /// <summary>Publishes a status message for workspace feature views.</summary>
        void SetStatus(string message);
        #endregion END Public
    }
}
