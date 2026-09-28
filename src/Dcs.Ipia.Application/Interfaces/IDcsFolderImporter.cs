namespace Dcs.Ipia.Application.Interfaces
{
    /// <summary>Reads a local DCS Saved Games joystick folder without modifying its files.</summary>
    public interface IDcsFolderImporter
    {
        #region BEGIN Public
        /// <summary>Detects a joystick folder or a module input directory containing one.</summary>
        /// <param name="folder">The selected local directory.</param>
        /// <returns>True when profile Lua files are present.</returns>
        bool HasProfiles(string folder);
        /// <summary>Reads profile Lua files and an adjacent modifiers.lua file.</summary>
        /// <param name="folder">The selected joystick or module input directory.</param>
        /// <param name="cancellationToken">Cancellation token for source reads.</param>
        /// <returns>Parsed profiles, bindings, modifiers, and diagnostics.</returns>
        Task<DcsFolderImportResult> ImportAsync(string folder, CancellationToken cancellationToken = default);
        #endregion END Public
    }
}
