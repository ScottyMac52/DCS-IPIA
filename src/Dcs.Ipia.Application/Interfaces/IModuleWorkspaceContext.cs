namespace Dcs.Ipia.Application.Interfaces
{
    /// <summary>Shares workspace state, edit operations and service boundaries among feature views.</summary>
    public interface IModuleWorkspaceContext
    {
        #region BEGIN Public
        /// <summary>Gets the current personal assignment labels keyed by slot identity.</summary>
        IReadOnlyDictionary<string, string> Labels { get; }
        /// <summary>Sets or clears a personal assignment label and refreshes workspace state.</summary>
        void SetLabel(string key, string? value);
        /// <summary>Gets the current editable workspace snapshot, if available.</summary>
        EditorWorkspaceSnapshot? Working { get; }
        /// <summary>Gets the assignment draft service for pending edits.</summary>
        DraftAssignmentService? Draft { get; }
        /// <summary>Gets the most recently saved workspace snapshot, if available.</summary>
        EditorWorkspaceSnapshot? Saved { get; }

        /// <summary>Gets the optional saved workspace repository.</summary>
        IEditorWorkspaceRepository? Repository { get; }
        /// <summary>Gets the connected-device discovery boundary.</summary>
        IConnectedDeviceDiscovery DeviceDiscovery { get; }
        /// <summary>Gets the optional adopted hardware inventory manager.</summary>
        HardwareInventoryManager? Inventory { get; }
        /// <summary>Gets the optional immutable hardware drawing library.</summary>
        IUserMappingLibrary? UserMappings { get; }
        /// <summary>Gets an optional adapter for confirming a reviewed drawing change.</summary>
        Func<MappingRevisionReview, bool>? MappingReviewConfirmation { get; }
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

        /// <summary>Occurs when the shared workspace state changes.</summary>
        event EventHandler? StateChanged;

        /// <summary>Occurs when a feature publishes a workspace status message.</summary>
        event EventHandler<string>? StatusChanged;

        /// <summary>Stages a structural workspace edit using the shared draft workflow.</summary>
        void RunStructureEdit(Func<EditorWorkspaceSnapshot, EditorWorkspaceSnapshot> edit);
        /// <summary>Saves the current workspace draft to its repository.</summary>
        Task SaveAsync();
        /// <summary>Discards pending workspace edits.</summary>
        void Discard();
        /// <summary>Publishes a status message for workspace feature views.</summary>
        void SetStatus(string message);
        #endregion END Public
    }
}
