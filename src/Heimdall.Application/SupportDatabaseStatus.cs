namespace Heimdall.Application
{
    /// <summary>Read-only database evidence without filenames or raw database content.</summary>
    /// <param name="SchemaVersion">Observed schema version, when readable.</param>
    /// <param name="IntegrityPassed">Whether SQLite quick_check and foreign_key_check passed.</param>
    public sealed record SupportDatabaseStatus(int? SchemaVersion, bool IntegrityPassed);
}

