namespace Dcs.Ipia.Domain
{
    /// <summary>Identifies a module or application release version.</summary>
    /// <param name="Major">The major.</param>
    /// <param name="Minor">The minor.</param>
    /// <param name="Patch">The patch.</param>
    /// <param name="Revision">The revision.</param>
    public sealed record ReleaseVersion(int Major, int Minor, int Patch, int Revision = 0)
    {
        #region BEGIN Public
        /// <summary>Formats the version.</summary>
        /// <returns>The result of the operation.</returns>
        public override string ToString() =>
            Revision == 0 ? $"v{Major}.{Minor}.{Patch}" : $"v{Major}.{Minor}.{Patch}.{Revision}";
        #endregion END Public
    }
}
