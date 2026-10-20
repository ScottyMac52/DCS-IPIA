namespace Heimdall.Infrastructure
{
    /// <summary>Versioned manifest of the complete saved user-data payload.</summary>
    /// <param name="FormatVersion">Archive format revision.</param>
    /// <param name="ApplicationVersion">Creating application version.</param>
    /// <param name="DatabaseSchemaVersion">Database migration version.</param>
    /// <param name="Files">Every payload file, keyed by safe relative path.</param>
    internal sealed record UserDataBackupManifest(int FormatVersion, string ApplicationVersion,
        int DatabaseSchemaVersion, Dictionary<string, UserDataBackupFile> Files);
}
