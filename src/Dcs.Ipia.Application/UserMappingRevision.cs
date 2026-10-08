namespace Dcs.Ipia.Application
{
    /// <summary>Content-addressed, portable evidence for one immutable user-authored mapping.</summary>
    /// <param name="Id">Hash of the complete revision.</param>
    /// <param name="SourceName">Original editable source filename.</param>
    /// <param name="ApplicationVersion">Importing application version.</param>
    /// <param name="Definition">Verified native target controls.</param>
    /// <param name="Source">Original native or compressed draw.io text.</param>
    /// <param name="Assets">Explicit local raster assets, keyed by safe relative path.</param>
    /// <param name="Svg">Deterministic derived rendering.</param>
    /// <param name="Width">Derived canvas width.</param>
    /// <param name="Height">Derived canvas height.</param>
    /// <param name="Controls">Complete controls, including generic fallbacks.</param>
    /// <param name="MappedInputs">Explicitly drawn DCS inputs.</param>
    /// <param name="SourceSha256">Source integrity evidence.</param>
    /// <param name="SvgSha256">Derived rendering integrity evidence.</param>
    public sealed record UserMappingRevision(string Id, string SourceName, string ApplicationVersion,
        HardwareDefinition Definition, string Source, IReadOnlyDictionary<string, byte[]> Assets,
        string Svg, double Width, double Height, IReadOnlyList<HardwareGraphicControl> Controls,
        IReadOnlyList<string> MappedInputs, string SourceSha256, string SvgSha256)
    {
        #region BEGIN Public
        /// <summary>Gets the versioned import contract; older modules omit this entire optional revision.</summary>
        public int FormatVersion { get; init; } = 1;
        /// <summary>Gets the fixed exporter semantics used to reproduce this revision.</summary>
        public string ExporterVersion { get; init; } = "ipia-drawio-1";
        /// <summary>Gets a distinguishable revision title for explicit selection.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public string Title => SourceName + " · " + Id[..12];
        #endregion END Public
    }
}
