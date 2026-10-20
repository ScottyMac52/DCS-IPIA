namespace Dcs.Ipia.EliteDangerous
{
    /// <summary>Display hints for known native command families; unfamiliar commands retain an Other category.</summary>
    public static class EliteCommandCategories
    {
        #region BEGIN Public
        /// <summary>Gets a display-only category without modifying a native command ID or its assignments.</summary>
        public static string ForCommand(string name) => Match(name)?.Category ?? "Other";
        #endregion END Public
        #region BEGIN private helpers
        private static readonly (string Section, string Category, string[] Prefixes)[] Families =
        [
            ("On Foot", "Communication", ["QuickCommsPanel_Humanoid"]),
            ("On Foot", "Combat", ["HumanoidPrimaryFire", "HumanoidZoom", "HumanoidMelee", "HumanoidThrowGrenade"]),
            ("On Foot", "Weapons & Equipment", ["HumanoidSelect", "HumanoidSwitch", "HumanoidCycle", "HumanoidReload", "HumanoidToggle", "HumanoidUse"]),
            ("On Foot", "Interaction", ["HumanoidInteract", "HumanoidSecondaryInteract", "HumanoidAccessPanel", "HumanoidItemWheel", "HumanoidEmote"]),
            ("On Foot", "Movement", ["HumanoidForward", "HumanoidBackward", "HumanoidStrafe", "HumanoidRotate", "HumanoidPitch", "HumanoidJump", "HumanoidSprint", "HumanoidWalk", "HumanoidCrouch"]),
            ("SRV", "Turret & Weapons", ["BuggyTurret", "BuggyPrimaryFire", "BuggySecondaryFire", "ToggleBuggyTurret"]),
            ("SRV", "Targeting", ["SelectTarget_Buggy", "SelectHighestThreat_Buggy", "CycleNextTarget_Buggy", "CyclePreviousTarget_Buggy"]),
            ("SRV", "Power Management", ["IncreaseEnginesPower_Buggy", "IncreaseWeaponsPower_Buggy", "IncreaseSystemsPower_Buggy", "ResetPowerDistribution_Buggy"]),
            ("SRV", "Driving", ["BuggyPitch", "BuggyRoll", "Drive", "Steering", "AutoBreakBuggy", "VerticalThrustersButton"]),
            ("SRV", "Utility", ["ToggleCargoScoop_Buggy", "HeadlightsBuggy", "EjectAllCargo_Buggy", "RecallDismissShip"]),
            ("General", "Interface", ["UI_", "FocusLeftPanel", "FocusRightPanel", "FocusRadarPanel", "FocusCommsPanel", "FocusRolePanel", "UIFocus", "Pause"]),
            ("General", "Maps", ["Galaxy", "SystemMap", "CamPitch", "CamYaw", "CamZoom", "CamTranslate"]),
            ("General", "Camera Suite", ["PhotoCamera", "VanityCamera", "FreeCam", "ToggleFreeCam", "FixCamera"]),
            ("General", "Headlook", ["HeadLook", "Headlook", "MouseHeadlook"]),
            ("General", "Communication", ["QuickComms", "Comms", "PushToTalk", "MicrophoneMute"]),
            ("General", "Multicrew", ["MultiCrew", "MultiCharge", "Crew"]),
            ("Ship", "Flight Rotation", ["Yaw", "Roll", "Pitch"]),
            ("Ship", "Flight Thrust", ["LateralThrust", "VerticalThrust", "AheadThrust", "BackwardThrust", "LeftThrust", "RightThrust", "UpThrust", "DownThrust", "Thrust"]),
            ("Ship", "Flight Throttle", ["Throttle", "SetSpeed", "ForwardKey", "BackwardKey", "SpeedInc", "SpeedDec"]),
            ("Ship", "Flight Assistance", ["ToggleFlight", "FlightAssist", "UseBoostJuice", "ToggleRotationCorrect"]),
            ("Ship", "Targeting", ["SelectTarget", "SelectHighestThreat", "TargetWingman", "WingNavLock", "CycleNextTarget", "CyclePreviousTarget", "CycleNextHostile", "CyclePreviousHostile", "CycleNextSubsystem", "CyclePreviousSubsystem"]),
            ("Ship", "Weapons", ["PrimaryFire", "SecondaryFire", "DeployHardpoint", "CycleFireGroup"]),
            ("Ship", "Power Management", ["IncreaseEnginesPower", "IncreaseWeaponsPower", "IncreaseSystemsPower", "ResetPowerDistribution"]),
            ("Ship", "Navigation & Travel", ["Hyper", "Supercruise", "Hyperspace", "TargetNextRouteSystem"]),
            ("Ship", "Landing & Cargo", ["LandingGear", "ToggleCargoScoop", "EjectAllCargo", "ShipSpotLight"]),
            ("Ship", "Cooling & Countermeasures", ["UseHeatSink", "DeployHeatSink", "UseShieldCell", "FireChaff", "DeployChaff"]),
            ("Ship", "Scanning", ["Exploration", "FSS", "SAA"]),
            ("Ship", "Fighter Orders", ["OrderRequest", "OrderDefensive", "OrderAggressive", "OrderFocusTarget", "OrderFollow", "OrderHoldFire", "OrderHoldPosition"])
        ];
        internal static string? SectionForCommand(string name) => Match(name)?.Section;
        private static (string Section, string Category)? Match(string name)
        {
            if (name.EndsWith("_Landing", StringComparison.Ordinal)) return ("Ship", "Landing Overrides");
            var required = name.Contains("Buggy", StringComparison.Ordinal) ? "SRV" : null;
            if (name.StartsWith("Humanoid", StringComparison.Ordinal) || name.EndsWith("_Humanoid", StringComparison.Ordinal)) required = "On Foot";
            foreach (var family in Families)
                if ((required is null || family.Section == required) && family.Prefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
                    return (family.Section, family.Category);
            return null;
        }
        #endregion END private helpers
    }
}
