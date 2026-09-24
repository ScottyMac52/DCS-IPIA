namespace Dcs.Ipia.Application;

/// <summary>Supplies one unmodified DCS-Common Lua source file.</summary>
/// <param name="Path">The repository-relative path.</param>
/// <param name="Content">The source text.</param>
public sealed record SharedLuaSource(string Path,string Content);
