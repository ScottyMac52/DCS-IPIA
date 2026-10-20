namespace Heimdall.EliteDangerous
{
    /// <summary>Display-only section hints; All always exposes every captured native command.</summary>
    public static class EliteCommandSections
    {
        private static readonly string[] ShipPrefixes = ["Yaw", "Roll", "Pitch", "Throttle", "Thrust", "SetSpeed", "TargetWingman", "Cycle", "Focus", "Increase", "Deploy", "ToggleFlight", "Hyper", "Supercruise", "Hyperspace", "Exploration", "SAA"];
        private static readonly HashSet<string> ShipCommands = new(StringComparer.Ordinal)
        { "PrimaryFire", "SecondaryFire", "SelectTarget", "SelectHighestThreat", "LandingGearToggle", "UseBoostJuice", "ToggleCargoScoop", "FlightAssist", "ResetPowerDistribution" };
        #region BEGIN Public
        /// <summary>Gets a conservative section; unmatched names remain visible under General and All.</summary>
        public static string ForCommand(string name)
        {
            if (EliteCommandCategories.SectionForCommand(name) is { } section) return section;
            if (name.StartsWith("Humanoid", StringComparison.Ordinal) || name.EndsWith("_Humanoid", StringComparison.Ordinal)) return "On Foot";
            if (name.Contains("Buggy", StringComparison.Ordinal) || name.StartsWith("Drive", StringComparison.Ordinal) || name.StartsWith("Steering", StringComparison.Ordinal) || name == "VerticalThrustersButton") return "SRV";
            return ShipCommands.Contains(name) || ShipPrefixes.Any(x => name.StartsWith(x, StringComparison.Ordinal)) ? "Ship" : "General";
        }
        #endregion END Public
    }
}
