namespace Heimdall.Application.Interfaces
{
    /// <summary>Owns native controller handles for one foreground test session.</summary>
    public interface IDeviceInputSource : IDisposable
    {
        #region BEGIN Public
        /// <summary>Gets supported controls by exact physical instance.</summary>
        IReadOnlyDictionary<Guid, IReadOnlyCollection<string>> SupportedInputs { get; }
        /// <summary>Reads all acquired controllers or fails the entire sample.</summary>
        IReadOnlyList<DeviceInputFrame> Read();
        #endregion END Public
    }
}
