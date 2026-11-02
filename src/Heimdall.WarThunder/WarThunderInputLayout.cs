using System.Globalization;
using Heimdall.Application;

namespace Heimdall.WarThunder
{
    /// <summary>Translates a neutral object roster to ordinary DirectInput native slots independently of DCS-supported controls.</summary>
    public sealed record WarThunderInputLayout(IReadOnlyList<WarThunderInputSlot> Axes, IReadOnlyList<WarThunderInputSlot> Buttons,
        IReadOnlyList<WarThunderInputTrace> Trace, bool AxesComplete, bool ButtonsComplete)
    {
        private const int ButtonLimit = 496; // JoystickRawState's 512 buttons minus 16 reserved POV buttons.
        private const int HatLimit = 4;
        private static readonly string[] Directions = ["U", "R", "D", "L"];
        #region BEGIN Public
        /// <summary>Retains native enumeration order; applies driver inclusion rules before validating saved range counts.</summary>
        public static WarThunderInputLayout Create(ControllerInputCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            if (catalog.SchemaVersion != 1) throw new InvalidDataException("Unsupported reported input catalog version.");
            var axes = new List<WarThunderInputSlot>(); var buttons = new List<WarThunderInputSlot>();
            var hats = new List<ReportedControllerInput>(); var trace = new List<WarThunderInputTrace>();
            var resolver = new WarThunderAxisLayout();
            var axesComplete = true; var buttonsComplete = true;
            foreach (var input in catalog.Objects)
            {
                switch (input.Kind)
                {
                    case ReportedInputKind.Axis:
                        var resolution = resolver.Resolve(input);
                        axesComplete &= resolution.Complete;
                        trace.Add(new(input.Id, input.EnumerationIndex, "Axis", resolution.StorageKey is null ? null : axes.Count, resolution.Detail));
                        if (resolution.StorageKey is not null) axes.Add(new(input.Id, axes.Count, [resolution.StorageKey], false));
                        break;
                    case ReportedInputKind.Button:
                        buttonsComplete &= !input.Ambiguous;
                        AddButton(input, buttons, trace);
                        break;
                    case ReportedInputKind.Hat:
                        axesComplete &= !input.Ambiguous; buttonsComplete &= !input.Ambiguous;
                        if (hats.Count < HatLimit) hats.Add(input);
                        else trace.Add(new(input.Id, input.EnumerationIndex, "Hat", null, "Excluded: driver POV limit reached."));
                        break;
                    default:
                        trace.Add(new(input.Id, input.EnumerationIndex, "Other", null, "Excluded: native object is not an axis, button or POV."));
                        break;
                }
            }
            AddHats(hats, axes, buttons, trace);
            return new(axes, buttons, trace, axesComplete, buttonsComplete);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void AddButton(ReportedControllerInput input, List<WarThunderInputSlot> buttons, List<WarThunderInputTrace> trace)
        {
            if (buttons.Count == ButtonLimit)
            {
                trace.Add(new(input.Id, input.EnumerationIndex, "Button", null, "Excluded: driver button limit reached."));
                return;
            }
            trace.Add(new(input.Id, input.EnumerationIndex, "Button", buttons.Count, "Button slot follows original enumeration, not its instance number."));
            buttons.Add(new(input.Id, buttons.Count, ["JOY_BTN" + (input.Instance + 1).ToString(CultureInfo.InvariantCulture)], false));
        }
        private static void AddHats(List<ReportedControllerInput> hats, List<WarThunderInputSlot> axes,
            List<WarThunderInputSlot> buttons, List<WarThunderInputTrace> trace)
        {
            foreach (var hat in hats)
            {
                var key = "JOY_BTN_POV" + (hat.Instance + 1).ToString(CultureInfo.InvariantCulture) + "_";
                trace.Add(new(hat.Id, hat.EnumerationIndex, "Hat", axes.Count, "POV appends horizontal/vertical native axes and four U/R/D/L button slots."));
                axes.Add(new(hat.Id + "/H", axes.Count, [key + "R", key + "L"], true));
                axes.Add(new(hat.Id + "/V", axes.Count, [key + "U", key + "D"], true));
                foreach (var direction in Directions) buttons.Add(new(hat.Id + "/" + direction, buttons.Count, [key + direction], true));
            }
        }
        #endregion END private helpers
    }
}
