namespace Heimdall.Application
{
    /// <summary>Verified archive version, schema and file-count information for user review.</summary>
    /// <param name="ApplicationVersion">Application that created the archive.</param>
    /// <param name="DatabaseSchemaVersion">Highest recorded database migration.</param>
    /// <param name="FileCount">Number of payload files.</param>
    public sealed record UserDataBackupInfo(string ApplicationVersion, int DatabaseSchemaVersion, int FileCount);
}
