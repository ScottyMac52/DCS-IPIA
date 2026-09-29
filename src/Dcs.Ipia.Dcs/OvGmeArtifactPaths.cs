namespace Dcs.Ipia.Dcs
{
    /// <summary>Paths and digest of a locally written OvGME archive.</summary>
    public sealed record OvGmeArtifactPaths(string ArchivePath, string ChecksumPath, string Sha256);
}
