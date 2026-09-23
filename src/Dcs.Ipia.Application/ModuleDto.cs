using Dcs.Ipia.Domain;
namespace Dcs.Ipia.Application;

/// <summary>Represents module dto.</summary>
/// <param name="Id">The identifier.</param>
/// <param name="DcsId">The DCS identifier.</param>
/// <param name="DisplayName">The display name.</param>
public sealed record ModuleDto(Guid Id,string DcsId,string DisplayName);
