namespace Heimdall.Application.Interfaces
{
    /// <summary>Preserves typed command, record and native filter models behind the common browser lifecycle.</summary>
    public interface IGameAssignmentBrowser<TCommand, TRecord, TFilter> : IGameAssignmentBrowser
    {
        #region BEGIN Public
        /// <summary>Gets the visible native commands in native projection order.</summary>
        IReadOnlyList<TCommand> Commands { get; }
        /// <summary>Gets the visible native records.</summary>
        IReadOnlyList<TRecord> Records { get; }
        /// <summary>Gets the current native filter.</summary>
        TFilter Filter { get; }
        /// <summary>Updates filters and search atomically, producing at most one final projection.</summary>
        void Query(TFilter filter, string text, string? preferredCommandId = null);
        /// <summary>Updates the native filter through the common refresh sequence.</summary>
        void SetFilter(TFilter filter);
        #endregion END Public
    }
}

