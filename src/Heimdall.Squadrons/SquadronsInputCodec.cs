using Heimdall.Domain.MultiSim;

namespace Heimdall.Squadrons
{
    /// <summary>Decodes verified joystick codes, including extended axes stored in the native button field.</summary>
    public static class SquadronsInputCodec
    {
        private static readonly Dictionary<(int Axis, int Button), SquadronsInput> Inputs = BuildInputs();
        #region BEGIN Public
        /// <summary>Maps native POV spelling to the shared enumerated key; axes and buttons already use shared keys.</summary>
        public static string PhysicalKey(string key) => key.Replace("JOY_POV", "JOY_BTN_POV", StringComparison.Ordinal);
        /// <summary>Gets independent supported inputs for a desktop picker.</summary>
        public static IReadOnlyList<SquadronsInput> Choices => Inputs.Values.ToArray();
        /// <summary>Resolves one physical drawing callout to its native input without exposing
        /// codec direction variants as separate physical controls. Existing axis direction
        /// is preserved when rebinding the same physical axis; new axes use the canonical
        /// positive code. This does not change the native record's negate flag.</summary>
        public static SquadronsInput FromDrawing(string physicalKey, bool isAxis,
            SquadronsInput? existing = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(physicalKey);
            var matches = Choices.Where(x => x.PhysicalKey == physicalKey).ToArray();
            if (matches.Length == 1 && (matches[0].Type == CommandType.Axis) == isAxis)
                return matches[0];
            if (isAxis && matches.Length == 2 && matches.All(x => x.Type == CommandType.Axis) &&
                matches.Count(x => x.Direction == 1) == 1 && matches.Count(x => x.Direction == -1) == 1)
            {
                var preservedDirection = existing?.Type == CommandType.Axis &&
                    existing.PhysicalKey == physicalKey ? existing.Direction : 1;
                return matches.Single(x => x.Direction == preservedDirection);
            }
            throw new InvalidOperationException("This drawing control has no unique supported Squadrons input.");
        }
        /// <summary>Returns null for unassigned or unknown input encodings, which remain native evidence.</summary>
        public static SquadronsInput? Decode(int axis, int button) => Inputs.GetValueOrDefault((axis, button));
        /// <summary>Encodes one supported input without guessing a physical button number from a native code.</summary>
        public static (int Axis, int Button) Encode(SquadronsInput input)
        {
            ArgumentNullException.ThrowIfNull(input);
            foreach (var pair in Inputs)
                if (pair.Value == input) return pair.Key;
            throw new InvalidOperationException("Unsupported Squadrons joystick input or direction.");
        }
        #endregion END Public
        #region BEGIN private helpers
        private static Dictionary<(int Axis, int Button), SquadronsInput> BuildInputs()
        {
            var inputs = new Dictionary<(int, int), SquadronsInput>();
            inputs.Add((8, 174), new("JOY_X", CommandType.Axis, 1));
            inputs.Add((10, 174), new("JOY_X", CommandType.Axis, -1));
            inputs.Add((9, 174), new("JOY_Y", CommandType.Axis, -1));
            inputs.Add((11, 174), new("JOY_Y", CommandType.Axis, 1));
            foreach (var (key, code) in new[] { ("JOY_SLIDER1", 14), ("JOY_SLIDER2", 16), ("JOY_Z", 40), ("JOY_RX", 42), ("JOY_RY", 44), ("JOY_RZ", 46) })
            {
                inputs.Add((26, code), new(key, CommandType.Axis, 1));
                inputs.Add((26, code + 1), new(key, CommandType.Axis, -1));
            }
            for (var number = 1; number <= 128; number++)
                inputs.Add((26, number <= 18 ? number + 21 : number + 45), new("JOY_BTN" + number, CommandType.Button, 0));
            var directions = new[] { "U", "D", "L", "R" };
            for (var hat = 1; hat <= 4; hat++)
                for (var direction = 0; direction < directions.Length; direction++)
                    inputs.Add((26, 48 + (hat - 1) * 4 + direction), new("JOY_POV" + hat + "_" + directions[direction], CommandType.Hat, 0));
            return inputs;
        }
        #endregion END private helpers
    }
}
