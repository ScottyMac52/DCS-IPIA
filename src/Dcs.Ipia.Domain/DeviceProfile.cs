using System.Security.Cryptography;
using System.Text;

namespace Dcs.Ipia.Domain
{
    /// <summary>Identifies a configured device instance.</summary>
    public sealed record DeviceProfile
    {
        /// <summary>Initializes a new device profile.</summary>
        /// <param name="id">The identifier.</param>
        /// <param name="deviceId">The device identifier.</param>
        /// <param name="instance">The instance.</param>
        /// <param name="role">The role.</param>
        /// <param name="sourceName">The source name.</param>
 public DeviceProfile(Guid id,string deviceId,string? instance,string role,string sourceName)
 {
     if (id == Guid.Empty)
         throw new ArgumentException("A profile ID is required.", nameof(id));
     Id = id;
     DeviceId = Required(deviceId, nameof(deviceId));
     Instance = Optional(instance);
     Role = Required(role, nameof(role));
     SourceName = Required(sourceName, nameof(sourceName));
 }
        #region BEGIN Public
 /// <summary>Gets the identifier.</summary>
 public Guid Id { get; }
 /// <summary>Gets the device identifier.</summary>
 public string DeviceId { get; }
 /// <summary>Gets the instance.</summary>
 public string ? Instance { get; }
 /// <summary>Gets the role.</summary>
 public string Role { get; }
 /// <summary>Gets the source name.</summary>
 public string SourceName { get; }
        #endregion END Public
        #region BEGIN private helpers
 private static string Required(string value, string name) =>
     string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A value is required.", name) : value.Trim();
 private static string ? Optional(string ? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        #endregion END private helpers
    }
}
