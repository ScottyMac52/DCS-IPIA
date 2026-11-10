using System.IO;
using Heimdall.App.Games.Profiles;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Domain.MultiSim;
using Heimdall.Infrastructure;
using Heimdall.WarThunder;

namespace Heimdall.App.Games.WarThunder
{
    /// <summary>A bounded, already-parsed War Thunder capture; parsing happens before the leave decision so an invalid file never discards edits.</summary>
    internal sealed record WarThunderImportRequest(string FullPath, ProfileDocument Profile)
    {
        /// <summary>Gets the native capture size limit.</summary>
        internal const long MaximumBytes = 8 * 1024 * 1024;
        /// <summary>Reads a capture through the native backend, enforcing the 8 MiB limit.</summary>
        internal static WarThunderImportRequest Read(WarThunderProfileBackend backend, string path)
        {
            var full = Path.GetFullPath(path);
            if (new FileInfo(full).Length > MaximumBytes) throw new InvalidDataException("Controls capture exceeds 8 MiB.");
            return new(full, backend.Import("controls", new Dictionary<string, string> { [Path.GetFileName(full)] = File.ReadAllText(full) }));
        }
    }
}
