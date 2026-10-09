namespace Dcs.Ipia.Application
{
    /// <summary>Explicit allow-list of support evidence; contains no source user records.</summary>
    /// <param name="FormatVersion">Support format version.</param>
    /// <param name="ApplicationVersion">Four-part application version.</param>
    /// <param name="RuntimeVersion">Runtime version without machine information.</param>
    /// <param name="SupportedSchemaVersion">Highest schema accepted by this application.</param>
    /// <param name="Database">Read-only database evidence, or null on failure.</param>
    /// <param name="Devices">Saved inventory evidence, or null when unreadable.</param>
    /// <param name="Modules">Readable saved module evidence, or null when listing failed.</param>
    /// <param name="CustomMappingRevisions">Verified library count, or null on failure.</param>
    /// <param name="Failures">Partial failures and next steps.</param>
    public sealed record SupportReport(int FormatVersion, string ApplicationVersion, string RuntimeVersion,
        int SupportedSchemaVersion, SupportDatabaseStatus? Database, IReadOnlyList<SupportDeviceStatus>? Devices,
        IReadOnlyList<SupportModuleStatus>? Modules, int? CustomMappingRevisions, IReadOnlyList<SupportFailure> Failures);
}

