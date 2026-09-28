using System.Security.Cryptography;
using System.Text;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Verifies the complete editor state can be frozen and restored without a database.</summary>
    public sealed class EditorSnapshotSerializerTests
    {
        private static readonly Guid ModuleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid ProfileId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        #region BEGIN Public
        /// <summary>Source ordering does not affect bytes; assignments and assets survive round-trip.</summary>
        [Fact]
        public void Canonical_workspace_round_trips()
        {
            var first = Workspace();
            var reversed = first with {
                Pages = first.Pages.Reverse().ToArray(), ModuleCommands = first.ModuleCommands.Reverse().ToArray(),
                Labels = new Dictionary<string, string> { { "z", "End" },
                                                          { first.Assignments[0].Slot.Key, "Edited" },
                                                          { "a", "Start" } },
                Sources = new Dictionary<string, string> { { "config/z.json", "z" }, { "config/a.json", "a" } }
            };
            var snapshot = EditorSnapshotSerializer.Serialize(first);
            Assert.Equal(snapshot, EditorSnapshotSerializer.Serialize(reversed));
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot.Content))),
                         snapshot.Sha256);
            Assert.Contains("\"assignments\"", snapshot.Content, StringComparison.Ordinal);
            Assert.Contains("\"sources\"", snapshot.Content, StringComparison.Ordinal);
            Assert.EndsWith("\n", snapshot.Content, StringComparison.Ordinal);
            var restored = EditorSnapshotSerializer.Deserialize(snapshot);
            Assert.Equal(ModuleId, restored.Configuration.Module.Id);
            Assert.Equal(2, restored.Pages.Count);
            Assert.Single(restored.Images);
            Assert.Single(restored.Assignments);
            Assert.Equal("JOY_BTN1", restored.Assignments[0].Slot.InputKey);
            Assert.Equal("Edited", restored.Labels[restored.Assignments[0].Slot.Key]);
            Assert.Equal(snapshot, EditorSnapshotSerializer.Serialize(restored));
        }
        /// <summary>Byte edits, schema changes and noncanonical formatting are rejected.</summary>
        [Fact]
        public void Rejects_tampered_or_noncanonical_snapshot()
        {
            var snapshot = EditorSnapshotSerializer.Serialize(Workspace());
            Assert.Throws<InvalidDataException>(
                () => EditorSnapshotSerializer.Deserialize(snapshot with { Content = snapshot.Content + " " }));
            var future =
                snapshot.Content.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 77", StringComparison.Ordinal);
            Assert.Throws<System.Text.Json.JsonException>(() => EditorSnapshotSerializer.Deserialize(Rehash(future)));
            Assert.Throws<InvalidDataException>(
                () => EditorSnapshotSerializer.Deserialize(Rehash(snapshot.Content + " ")));
        }
        /// <summary>Cross-module assignments and corrupt pinned images cannot be exported.</summary>
        [Fact]
        public void Rejects_foreign_or_corrupt_workspace()
        {
            var workspace = Workspace();
            Assert.Throws<ArgumentException>(() => EditorSnapshotSerializer.Serialize(workspace with { Assignments = [
                workspace.Assignments[0] with { Slot =
                                                    workspace.Assignments[0].Slot with { ModuleId = Guid.NewGuid() } }
            ] }));
            Assert.Throws<ArgumentException>(
                () => EditorSnapshotSerializer.Serialize(
                    workspace with { Pages = [workspace.Pages[0] with { ModuleId = Guid.NewGuid() }] }));
            Assert.Throws<ArgumentException>(
                () => EditorSnapshotSerializer.Serialize(
                    workspace with { Images = [workspace.Images[0] with { Bytes = [1, 2, 3] }] }));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static ImmutableSnapshot Rehash(string content) =>
            new(content, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content))));
        private static EditorWorkspaceSnapshot Workspace()
        {
            var module = new DcsModule(ModuleId, "UH-1H", "Huey");
            var profile = new DeviceProfile(ProfileId, "tm-mfd", "MFD1", "left", "MFD.diff.lua");
            var configuration = new ModuleConfiguration(module, [profile], [], [], "Huey@0123456789abcdef");
            var slot =
                new DraftAssignmentSlot(ModuleId, ProfileId, AssignmentLayer.Module, BindingSection.Key, "JOY_BTN1");
            var imageBytes = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
            var image = new KneeboardImageAsset("01-MFD", "kneeboard/UH-1H/01-MFD.png", imageBytes,
                                                Convert.ToHexStringLower(SHA256.HashData(imageBytes)));
            return new(configuration,
                       [
                           new(Guid.NewGuid(), ModuleId, ProfileId, KneeboardPageKind.Device, "02-MFD", "MFD 2", [], 2),
                           new(Guid.NewGuid(), ModuleId, ProfileId, KneeboardPageKind.Device, "01-MFD", "MFD 1", [], 1)
                       ],
                       [image], [new("TM MFD", "MFD1", "assets/shared/hardware/svg/tm-mfd.svg", 1, 1, [],
                                     []) { Svg = "<svg xmlns='http://www.w3.org/2000/svg'/>", ProfileId = ProfileId }],
                       [new("ZZ", "Last", "Other", false), new("AA", "First", "Other", false)], [],
                       [new(slot, new("AA", "First", false, AssignmentLayer.Module))],
                       new Dictionary<string, string> { { "a", "Start" }, { slot.Key, "Edited" }, { "z", "End" } },
                       new Dictionary<string, string> { { "config/a.json", "a" }, { "config/z.json", "z" } });
        }
        #endregion END private helpers
    }
}
