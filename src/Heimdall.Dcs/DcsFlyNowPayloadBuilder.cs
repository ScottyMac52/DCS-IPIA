using System.IO.Compression;
using Heimdall.Application;
using Heimdall.Application.Interfaces;

namespace Heimdall.Dcs
{
    /// <summary>Extracts only DCS payload files from independently verified frozen module output.</summary>
    /// <param name="workspaces">Saved module repository.</param>
    public sealed class DcsFlyNowPayloadBuilder(IDcsEditorWorkspaceRepository workspaces) : IDcsFlyNowPayloadBuilder
    {
        #region BEGIN Public
        /// <inheritdoc/>
        public async Task<DcsFlyNowPayload> BuildAsync(Guid moduleId, CancellationToken ct = default)
        {
            var saved = await workspaces.GetAsync(moduleId, ct)
                ?? throw new InvalidOperationException("The selected module is no longer in the database.");
            return await Task.Run(() =>
            {
                var frozen = new DcsModuleBuildSnapshot("FlyNow", DcsEditorSnapshotSerializer.Serialize(saved));
                var package = DcsModuleSnapshotWorkflow.Build(frozen);
                var prefix = package.FileName[..^4] + "/";
                using var stream = new MemoryStream(package.Bytes, false);
                using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
                var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in zip.Entries.Where(x => x.FullName.StartsWith(prefix, StringComparison.Ordinal)
                    && !x.FullName.EndsWith('/')))
                {
                    using var input = entry.Open();
                    using var output = new MemoryStream();
                    input.CopyTo(output);
                    files.Add(entry.FullName[prefix.Length..], output.ToArray());
                }
                return new DcsFlyNowPayload(saved.Configuration.Module.DisplayName, files);
            }, ct);
        }
        #endregion END Public
    }
}
