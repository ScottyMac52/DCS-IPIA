using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Heimdall.Domain;
using Heimdall.Dcs.SnapshotModels;

namespace Heimdall.Dcs
{
    /// <summary>Represents immutable snapshot.</summary>
    /// <param name="Content">The content.</param>
    /// <param name="Sha256">The sha256.</param>
    public sealed record ImmutableSnapshot(string Content, string Sha256);
}
