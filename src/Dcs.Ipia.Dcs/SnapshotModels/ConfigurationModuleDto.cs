using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Dcs.SnapshotModels;

/// <summary>Represents configuration module dto.</summary>
/// <param name="Id">The identifier.</param>
/// <param name="DcsId">The DCS identifier.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="Provenance">The provenance.</param>
/// <param name="Profiles">The profiles.</param>
/// <param name="Modifiers">The modifiers.</param>
/// <param name="Bindings">The bindings.</param>
internal sealed record ConfigurationModuleDto(Guid Id,string DcsId,string DisplayName,string Provenance,IReadOnlyList<ConfigurationProfileDto> Profiles,IReadOnlyList<ConfigurationModifierDto> Modifiers,IReadOnlyList<ConfigurationBindingDto> Bindings);
