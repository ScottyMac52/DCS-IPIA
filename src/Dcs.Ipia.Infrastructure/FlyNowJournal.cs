using Dcs.Ipia.Application;

namespace Dcs.Ipia.Infrastructure
{
    /// <summary>A durable copy intent with rollback bytes written before any DCS files change.</summary>
    /// <param name="Active">Candidate or completed active deployment.</param>
    /// <param name="Previous">Activation to restore after an interrupted copy.</param>
    /// <param name="BackupFolder">Operation directory below the configured backup root.</param>
    /// <param name="ReplacedFiles">Files in this copy attempt; existing ones have rollback backups.</param>
    /// <param name="Writing">Whether copying has not yet committed.</param>
    internal sealed record FlyNowJournal(FlyNowDeployment Active, FlyNowDeployment? Previous,
        string BackupFolder, IReadOnlyDictionary<string, bool> ReplacedFiles, bool Writing);
}
