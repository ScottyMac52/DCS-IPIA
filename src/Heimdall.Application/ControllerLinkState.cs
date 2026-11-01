namespace Heimdall.Application
{
    /// <summary>Shared Devices policy: saved associations require explicit unlink before replacement.</summary>
    public sealed record ControllerLinkState(bool SelectPhysical, bool UnlinkPhysical, bool SelectDrawing, bool UnlinkDrawing)
    {
        #region BEGIN Public
        /// <summary>Uses saved ownership, independently of whether the physical unit is currently connected.</summary>
        public static ControllerLinkState For(bool physicalLinked, bool drawingLinked, bool ready, bool busy)
        {
            if (!ready || busy) return new(false, false, false, false);
            return new(!physicalLinked, physicalLinked, physicalLinked && !drawingLinked, drawingLinked);
        }
        #endregion END Public
    }
}
