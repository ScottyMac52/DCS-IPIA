namespace Dcs.Ipia.Packaging
{
    /// <summary>A complete deterministic OvGME ZIP and its separate checksum record.</summary>
    /// <param name="FileName">Archive file name.</param>
    /// <param name="Bytes">ZIP bytes.</param>
    /// <param name="Sha256">Lowercase SHA-256 of the ZIP bytes.</param>
    /// <param name="ChecksumLine">Line suitable for a separate SHA256SUMS.txt file.</param>
    public sealed record OvGmePackage(string FileName, byte[] Bytes, string Sha256, string ChecksumLine);
}
