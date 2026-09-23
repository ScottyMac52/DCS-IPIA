namespace Dcs.Ipia.Domain;

/// <summary>Represents source provenance.</summary>
/// <param name="Repository">The repository.</param>
/// <param name="Commit">The commit.</param>
/// <param name="Path">The path.</param>
/// <param name="Sha256">The sha256.</param>
public sealed record SourceProvenance(string Repository,string Commit,string Path,string Sha256);
