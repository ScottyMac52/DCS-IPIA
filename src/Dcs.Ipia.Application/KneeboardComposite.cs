namespace Dcs.Ipia.Application;

/// <summary>All current device labels and legend entries for one final-style kneeboard image.</summary>
/// <param name="Title">Authored kneeboard page title.</param>
/// <param name="Kicker">Authored subtitle.</param>
/// <param name="Labels">All staged label variants keyed by draw.io control ID.</param>
/// <param name="CategoryLabels">Authored category text keyed by SVG meta ID.</param>
/// <param name="Legend">Colors and modifiers used by the current draft.</param>
public sealed record KneeboardComposite(string Title,string Kicker,
 IReadOnlyDictionary<string,IReadOnlyList<KneeboardLabelVariant>> Labels,
 IReadOnlyDictionary<string,string> CategoryLabels,IReadOnlyList<KneeboardModifierLegend> Legend);
