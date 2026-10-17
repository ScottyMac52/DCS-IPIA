namespace Dcs.Ipia.Application
{
    /// <summary>Transports the exact signed UTF-8 claims and RSA-PSS signature as base64.</summary>
    /// <param name="Payload">Original serialized claims bytes.</param>
    /// <param name="Signature">RSA-PSS SHA256 signature.</param>
    public sealed record SignedOwnerLicense(string Payload, string Signature);
}
