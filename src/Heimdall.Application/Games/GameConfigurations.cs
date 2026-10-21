namespace Heimdall.Application.Games
{
    /// <summary>Creates independent named variants without inventing a game command catalog.</summary>
    public static class GameConfigurations
    {
        #region BEGIN Public
        /// <summary>Copies all controllers, evidence and settings; New additionally clears authored bindings.</summary>
        public static GameWorkspace Create(GameWorkspace source, string name, bool clearAssignments)
        {
            GameWorkspaceContract.Validate(source); GameConfigurationName.Validate(name);
            var copy = GameWorkspaceContract.Clone(source) with { Id = Guid.NewGuid(), Name = name };
            if (clearAssignments)
                foreach (var device in copy.Profile.Devices) { device.Buttons.Clear(); device.Axes.Clear(); }
            return copy;
        }
        #endregion END Public
    }
}
