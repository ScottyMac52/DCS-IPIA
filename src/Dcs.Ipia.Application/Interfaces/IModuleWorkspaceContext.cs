namespace Dcs.Ipia.Application.Interfaces
{
    public interface IModuleWorkspaceContext
    {
        IReadOnlyDictionary<string, string> Labels { get; }
        void SetLabel(string key, string? value);
        EditorWorkspaceSnapshot? Working { get; }
        DraftAssignmentService? Draft { get; }
        EditorWorkspaceSnapshot? Saved { get; }

        IEditorWorkspaceRepository? Repository { get; }
        IConnectedDeviceDiscovery DeviceDiscovery { get; }
        HardwareInventoryManager? Inventory { get; }
        IUserMappingLibrary? UserMappings { get; }
        Func<MappingRevisionReview, bool>? MappingReviewConfirmation { get; }
        Func<bool, IReadOnlyList<string>?>? CommandFilePicker { get; }
        Func<string?>? IntegrationFilePicker { get; }
        Func<nint, IReadOnlyList<Guid>, IDeviceInputSource>? InputSourceFactory { get; }
        Action<string, string>? InputLogClipboardWriter { get; }
        Func<string?>? AxisDefaultsFilePicker { get; }
        string SavedGamesRoot { get; set; }

        event EventHandler? StateChanged;

        event EventHandler<string>? StatusChanged;

        void RunStructureEdit(Func<EditorWorkspaceSnapshot, EditorWorkspaceSnapshot> edit);
        Task SaveAsync();
        void Discard();
        void SetStatus(string message);
    }
}
