namespace Heimdall.App.Games
{
    /// <summary>Transfers selection ownership synchronously when a cached workspace feature is mounted or replaced.</summary>
    internal interface IGameWorkspaceFeatureLifetime
    {
        #region BEGIN Public
        /// <summary>Activates the mounted feature before native preparation and releases it before another feature takes ownership.</summary>
        void SetFeatureActive(bool active);
        #endregion END Public
    }
}
