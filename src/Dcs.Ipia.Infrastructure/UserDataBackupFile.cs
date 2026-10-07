namespace Dcs.Ipia.Infrastructure
{
    /// <summary>Length and SHA-256 of one archived user-data file.</summary>
    /// <param name="Length">Uncompressed byte count.</param>
    /// <param name="Sha256">Uppercase SHA-256 digest.</param>
    internal sealed record UserDataBackupFile(long Length, string Sha256);
}
