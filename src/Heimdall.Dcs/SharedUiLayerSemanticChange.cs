namespace Heimdall.Dcs
{
    /// <summary>Review evidence for a default value compared with a module override.</summary>
    /// <param name="Kind">Binding, Catalog or Modifier.</param>
    /// <param name="Device">Device filename, or catalog.</param>
    /// <param name="Identity">Physical slot, command code or modifier name.</param>
    /// <param name="Decision">Apply inherited change or a preserved module difference.</param>
    /// <param name="Before">Current module value.</param>
    /// <param name="After">Proposed default value.</param>
    public sealed record SharedUiLayerSemanticChange(string Kind, string Device, string Identity,
        string Decision, string Before, string After);
}

