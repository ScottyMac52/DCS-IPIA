using System.Globalization;

namespace Heimdall.WarThunder;

/// <summary>Native control inventory; never treats global offsets as verified physical device inputs.</summary>
public sealed class WarThunderControls
{
    public WarThunderBlkDocument Document { get; }
    public IReadOnlyList<WarThunderBlkScalar> Hotkeys { get; }
    public IReadOnlyList<WarThunderBlkScalar> Axes { get; }
    public IReadOnlyList<WarThunderBlkScalar> Joysticks { get; }

    public WarThunderControls(string machineBlk)
    {
        Document = WarThunderBlkDocument.Parse(machineBlk);
        Hotkeys = Document.Scalars.Where(x => x.Path.Contains("/controls/hotkeys/", StringComparison.Ordinal)).ToArray();
        Axes = Document.Scalars.Where(x => x.Path.Contains("/controls/axes/", StringComparison.Ordinal)).ToArray();
        Joysticks = Document.Scalars.Where(x => x.Path.Contains("/controls/joysticks/", StringComparison.Ordinal)).ToArray();
    }

    /// <summary>Returns all captured numeric activation codes, without assuming their semantics.</summary>
    public IReadOnlyList<int> ActivationCodes() =>
        Hotkeys.Where(x => x.Path.EndsWith("/activationType", StringComparison.Ordinal) && x.Type == 'i')
            .Select(x => int.Parse(x.Value, CultureInfo.InvariantCulture)).Distinct().Order().ToArray();

    /// <summary>Changes a reviewed native setting; caller must verify device association first.</summary>
    public string ReplaceReviewedSetting(string path, string nativeValue) => Document.ReplaceScalar(path, nativeValue);
}
