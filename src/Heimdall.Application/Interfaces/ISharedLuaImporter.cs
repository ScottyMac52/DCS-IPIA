namespace Heimdall.Application.Interfaces
{
    /// <summary>Imports pinned DCS-Common hardware and canonical UI Layer Lua without writing source files.</summary>
    public interface ISharedLuaImporter
    {
        #region BEGIN Public
        /// <summary>Parses the supplied Lua texts and validates their shared catalog references.</summary>
        /// <param name="request">The pinned shared catalog and Lua source texts.</param>
        /// <returns>Normalized controls, profiles, bindings, modifiers, and diagnostics.</returns>
        SharedLuaImportResult Import(SharedLuaImportRequest request);
        #endregion END Public
    }
}
