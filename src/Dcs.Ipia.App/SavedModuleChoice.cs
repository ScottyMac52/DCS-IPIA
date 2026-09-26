namespace DcsIpia.Presentation;

/// <summary>Identifies one stored editor workspace in the module picker.</summary>
/// <param name="Id">Persisted module identity.</param>
/// <param name="DisplayName">Module name and DCS identity shown to the user.</param>
public sealed record SavedModuleChoice(Guid Id,string DisplayName);
