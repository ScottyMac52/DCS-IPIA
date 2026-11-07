using Heimdall.Application.Games;

namespace Heimdall.Application.Interfaces
{
    /// <summary>Native multi-sim session ownership used to reject retained references after a same-ID reopen.</summary>
    internal interface IGameProfileSessionOwner
    {
        #region BEGIN Public
        /// <summary>Gets the exact mounted native session instance.</summary>
        GameProfileSession? Session { get; }
        #endregion END Public
    }
}
