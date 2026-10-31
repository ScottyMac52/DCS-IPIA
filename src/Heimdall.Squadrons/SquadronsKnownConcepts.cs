namespace Heimdall.Squadrons
{
    /// <summary>Native command concept names found in captured Squadrons v4 profiles.
    /// This is a command-only reference, never a substitute for native binding records.</summary>
    internal static class SquadronsKnownConcepts
    {
        /// <summary>Flight camera labels reported by the game UI; native concept identities remain unchanged.</summary>
        internal static IReadOnlyDictionary<string, string> DisplayNames { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["IncomStarshipInputConcepts.ConceptCameraPitch"] = "Free Look Camera Up / Down (axis)",
            ["IncomStarshipInputConcepts.ConceptCameraYaw"] = "Free Look Camera Left / Right (axis)"
        };

        /// <summary>Retains commands when the user clears their final native binding record.</summary>
        internal static IReadOnlyCollection<string> All { get; } = Build();

        private static HashSet<string> Build()
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            Add(result, "IncomDefaultInputConcepts", """
                Activate ButtonSelect ButtonStart ConversationSkip Deactivate Edit Legend Menu MenuDigitalLeft MenuDigitalRight MenuExit MoveBackward MoveForward MoveLeft MoveRight PageLeft ParentNavigateLeft ParentNavigateRight PushTalk Ready ScrollDown ScrollUp SpectatorCameraFOVChange SpectatorCameraMovementZ SpectatorCameraPitch SpectatorCameraReset SpectatorCameraRoll SpectatorCameraRollActivate SpectatorCameraSwapPlayerMode SpectatorCameraYaw SpectatorChangeCategory SpectatorChangeRestriction SpectatorHudVisibility SpectatorLookAt SpectatorNextPOI SpectatorPreviousPOI StatusBar TeamChat ToggleChat VRRecenterCamera View
                """);
            Add(result, "IncomSoldierInputConcepts", """
                CameraPitch CameraYaw CommMenuX CommMenuY EmoteWheel Interact
                """);
            Add(result, "IncomStarshipInputConcepts", """
                Afterburner CameraPitch CameraYaw CommMenu CommMenuBattleCry CommMenuBoo CommMenuBrag CommMenuCheer CommMenuHelpMe CommMenuPraise CommMenuRegroup CommMenuSelect CommMenuThank CommMenuX CommMenuY Communication ContextualInteraction ControlBalancePower ControlEnginePower ControlShieldPower ControlWeaponPower Drift EmergencyPowerTransferBalance EmergencyPowerTransferEngine EmergencyPowerTransferWeapon Fire FireAuxiliaryWeaponOneDoubleTap FireAuxiliaryWeaponOneMain FireAuxiliaryWeaponTwoDoubleTap FireAuxiliaryWeaponTwoMain FireCountermeasure FreeLook FreeLookCameraDown FreeLookCameraLeft FreeLookCameraRight FreeLookCameraUp FreeLookTrigger IncreaseEnginePower IncreaseShieldPower IncreaseWeaponPower MaximizeEnginePower MaximizeShieldPower MaximizeWeaponPower MouseRecenter PingSelf Pitch PowerTransfer PowerTransferMenuSelect PowerTransferMenuX PowerTransferMenuY ResetSystemsPower Roll Scoreboard ShieldBack ShieldBalance ShieldFront TargetCycle TargetCycleNext TargetHighestThreat TargetPing TargetSelect Targeting TargetingMenu TargetingMenuCycleAllAllies TargetingMenuCycleAllEnemies TargetingMenuCycleEnemyAI TargetingMenuCycleEnemySquadron TargetingMenuCycleFlagshipSystems TargetingMenuCycleLastAttackers TargetingMenuCycleMissiles TargetingMenuCycleMySquadron TargetingMenuCycleObjectives TargetingMenuCycleTargetAttackers TargetingMenuSelectTargetingMethod TargetingMenuX TargetingMenuY Throttle Yaw
                """);
            return result;
        }

        private static void Add(HashSet<string> result, string scope, string names)
        {
            foreach (var name in names.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                result.Add(scope + ".Concept" + name);
        }
    }
}
