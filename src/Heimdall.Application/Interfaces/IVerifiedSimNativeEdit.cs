namespace Heimdall.Application.Interfaces
{
    /// <summary>An adapter-owned physical assignment that requires fresh linked-controller evidence.</summary>
    public interface IVerifiedSimNativeEdit : ISimNativeEdit
    {
        #region BEGIN Public
        /// <summary>Gets the native logical controller receiving the assignment.</summary>
        string LogicalDeviceId { get; }
        /// <summary>Gets the canonical physical key that must exist on the linked controller.</summary>
        string PhysicalInputKey { get; }
        #endregion END Public
    }
}
