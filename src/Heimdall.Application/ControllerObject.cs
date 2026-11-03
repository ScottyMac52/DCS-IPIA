namespace Heimdall.Application
{
    /// <summary>Native object evidence from DirectInput, before interpreting supported controls.</summary>
    /// <param name="ObjectGuid">Native object type GUID.</param>
    /// <param name="NativeType">Object category and instance encoded by DirectInput.</param>
    /// <param name="Name">Native object label.</param>
    public sealed record ControllerObject(Guid ObjectGuid, uint NativeType, string Name)
    {
        #region BEGIN Public
        /// <summary>Gets optional raw native metadata; absent in older persisted captures.</summary>
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public ControllerObjectDetails? Details { get; init; }
        #endregion END Public
    }
}
