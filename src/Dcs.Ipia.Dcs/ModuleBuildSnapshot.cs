namespace Dcs.Ipia.Dcs
{
    /// <summary>Portable package inputs containing the requested version and verified frozen editor state.</summary>
    /// <param name="Version">Version used for reproducible package generation.</param>
    /// <param name="Workspace">Canonical editor JSON and its SHA-256 digest.</param>
    public sealed record ModuleBuildSnapshot(string Version, ImmutableSnapshot Workspace);
}
