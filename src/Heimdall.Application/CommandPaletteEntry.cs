namespace Heimdall.Application
{
    /// <summary>One command displayed in a read-only, source-specific palette.</summary>
    /// <param name="Id">The source command identifier.</param>
    /// <param name="Name">The human-readable command name.</param>
    /// <param name="Category">The source category or binding section.</param>
    /// <param name="IsAxis">Whether the command represents an axis.</param>
    public sealed record CommandPaletteEntry(string Id, string Name, string Category, bool IsAxis)
    {
        #region BEGIN Public
        /// <summary>Gets a compact label with category and source command identifier.</summary>
        public string Display => $"{Name}  ·  {Category}  ·  {Id}";
        #endregion END Public
    }
}
