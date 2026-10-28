namespace Heimdall.Application.Games
{
    /// <summary>Explicitly advertised diagram actions. An unsupported native operation
    /// must never appear as a functional menu command.</summary>
    public readonly record struct HardwareDiagramActionAvailability(bool Select, bool Drop, bool Clear, bool Tune)
    {
        /// <summary>Action capabilities require both a real handler and a valid current target.</summary>
        public static HardwareDiagramActionAvailability For(bool selectHandler, bool dropHandler,
            bool clearHandler, bool clearTarget, bool tuneHandler, bool tuneTarget) =>
            new(selectHandler, dropHandler, clearHandler && clearTarget, tuneHandler && tuneTarget);
    }
}
