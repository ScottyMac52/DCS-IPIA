namespace Heimdall.Domain
{
    /// <summary>Identifies a major and minor domain schema version.</summary>
    /// <param name="Major">The major.</param>
    /// <param name="Minor">The minor.</param>
    public readonly record struct DomainSchemaVersion(int Major, int Minor)
    {
        #region BEGIN Public
        /// <summary>Formats the version.</summary>
        /// <returns>The result of the operation.</returns>
        public override string ToString() => $"{Major}.{Minor}";
        #endregion END Public
    }
}
