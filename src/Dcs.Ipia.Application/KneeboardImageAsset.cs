namespace Dcs.Ipia.Application;

/// <summary>Preserves one committed Huey kneeboard PNG for a read-only preview.</summary>
/// <param name="PageKey">The authored page filename without its extension.</param>
/// <param name="Path">Repository-relative committed PNG path.</param>
/// <param name="Bytes">Exact PNG bytes read from Git.</param>
/// <param name="Sha256">SHA-256 of the committed PNG bytes.</param>
public sealed record KneeboardImageAsset(string PageKey,string Path,byte[] Bytes,string Sha256);
