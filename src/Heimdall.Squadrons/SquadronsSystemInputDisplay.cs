namespace Heimdall.Squadrons
{
    /// <summary>Read-only display of native system input evidence. Numeric codes are intentionally
    /// not translated to OS keys or mouse buttons without a verified Squadrons mapping.</summary>
    public static class SquadronsSystemInputDisplay
    {
        /// <summary>Formats type, axis, and button codes without modifying native binding evidence.</summary>
        public static string Describe(SquadronsBindingRow row)
        {
            ArgumentNullException.ThrowIfNull(row);
            var label = row.Type switch
            {
                0 => "Mouse",
                1 => "Gamepad",
                2 => "Keyboard",
                3 => "Joystick (unrecognized input)",
                _ => "Native input type " + row.Type
            };
            var axis = row.Fields["axis"];
            var button = row.Fields["button"];
            return label + " · button code " + button + " · axis code " + axis;
        }
    }
}
