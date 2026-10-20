namespace Heimdall.App
{
    /// <summary>Describes a file or directory without holding an open filesystem handle.</summary>
    internal sealed record BrowseEntry(string Name, string FullPath, bool IsDirectory)
    {
        #region BEGIN Public
        /// <summary>Gets the accessible text identifying a directory or file.</summary>
        public string Kind => IsDirectory ? "Folder" : "File";
        #endregion END Public
    }
}
