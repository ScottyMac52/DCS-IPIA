using System.Text.Json;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Domain.MultiSim;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Protects workspace envelope, drawing ownership and game-specific native identity boundaries.</summary>
    public sealed class GameWorkspaceContractTests
    {
        #region BEGIN Public
        /// <summary>Every supported game accepts independent physical instances and cloning isolates mutable data.</summary>
        [Theory]
        [InlineData("elite-dangerous")][InlineData("star-wars-squadrons")][InlineData("war-thunder")]
        public void Valid_envelopes_and_clones_preserve_ownership(string backend)
        {
            var workspace = Workspace(backend); GameWorkspaceContract.Validate(workspace);
            var clone = GameWorkspaceContract.Clone(workspace);
            clone.Profile.Devices[0].LogicalId = "changed"; clone.Hardware.Clear(); clone.ControllerOrder.Add("changed");
            Assert.Equal("one", workspace.Profile.Devices[0].LogicalId); Assert.Equal(2, workspace.Hardware.Count); Assert.Empty(workspace.ControllerOrder);
        }
        /// <summary>Invalid envelopes are rejected without normalizing or mutating their supplied contents.</summary>
        [Theory]
        [InlineData("schema")][InlineData("id")][InlineData("name")][InlineData("long-name")]
        [InlineData("profile")][InlineData("hardware")][InlineData("order-null")][InlineData("drawings-null")]
        [InlineData("backend")][InlineData("devices-null")][InlineData("device-null")][InlineData("relative-source")]
        [InlineData("order-blank")][InlineData("order-duplicate")][InlineData("drawing-blank")][InlineData("drawing-physical")][InlineData("drawing-unknown")]
        [InlineData("pin-unknown")][InlineData("pin-null")][InlineData("device-pin-null")][InlineData("physical-empty")][InlineData("drawing-id-blank")]
        public void Invalid_workspace_boundaries_fail_without_changes(string failure)
        {
            var workspace = Invalid(Workspace("war-thunder"), failure); var before = JsonSerializer.Serialize(workspace);
            Assert.Throws<InvalidDataException>(() => GameWorkspaceContract.Validate(workspace));
            Assert.Equal(before, JsonSerializer.Serialize(workspace));
        }
        /// <summary>Native product and unique-slot rules apply to both captured-slot games, while Elite keeps its own policy.</summary>
        [Theory]
        [InlineData("star-wars-squadrons", false)][InlineData("war-thunder", false)][InlineData("elite-dangerous", true)]
        public void Native_slot_games_require_products_and_distinct_instances(string backend, bool permitted)
        {
            var missingProduct = Workspace(backend);
            missingProduct.Hardware["one"] = missingProduct.Hardware["one"] with { Device = missingProduct.Hardware["one"].Device with { ProductGuid = Guid.Empty } };
            var duplicate = Workspace(backend); duplicate.Hardware["two"] = duplicate.Hardware["one"];
            foreach (var workspace in new[] { missingProduct, duplicate })
                if (permitted) GameWorkspaceContract.Validate(workspace);
                else Assert.Throws<InvalidDataException>(() => GameWorkspaceContract.Validate(workspace));
        }
        /// <summary>Independent drawing pins can survive unlinking; relative paths and missing envelopes cannot.</summary>
        [Fact]
        public void Independent_drawing_and_absolute_source_are_valid()
        {
            Assert.Throws<ArgumentNullException>(() => GameWorkspaceContract.Validate(null!));
            var workspace = Workspace("war-thunder") with { SourceDirectory = Path.GetTempPath() };
            workspace.Hardware.Remove("one"); workspace.UnlinkedDrawings["one"] = "drawing";
            GameWorkspaceContract.Validate(workspace);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static GameWorkspace Workspace(string backend) => new(1, Guid.NewGuid(), "Controls",
            new ProfileDocument { BackendId = backend, Devices = [new() { LogicalId = "one" }, new() { LogicalId = "two" }] },
            new() { ["one"] = new("drawing", new(Guid.NewGuid(), "One", true, ProductGuid: Guid.NewGuid())),
                ["two"] = new("drawing", new(Guid.NewGuid(), "Two", false, ProductGuid: Guid.NewGuid())) });
        private static GameWorkspace Invalid(GameWorkspace workspace, string failure)
        {
            switch (failure)
            {
                case "schema": return workspace with { SchemaVersion = 2 };
                case "id": return workspace with { Id = Guid.Empty };
                case "name": return workspace with { Name = " " };
                case "long-name": return workspace with { Name = new string('x', 201) };
                case "profile": return workspace with { Profile = null! };
                case "hardware": return workspace with { Hardware = null! };
                case "order-null": return workspace with { ControllerOrder = null! };
                case "drawings-null": return workspace with { UnlinkedDrawings = null! };
                case "backend": workspace.Profile.BackendId = "unsupported"; break;
                case "devices-null": workspace.Profile.Devices = null!; break;
                case "device-null": workspace.Profile.Devices.Add(null!); break;
                case "relative-source": return workspace with { SourceDirectory = "relative" };
                case "order-blank": workspace.ControllerOrder.Add(" "); break;
                case "order-duplicate": workspace.ControllerOrder.AddRange(["one", "one"]); break;
                case "drawing-blank": workspace.Hardware.Remove("one"); workspace.UnlinkedDrawings["one"] = " "; break;
                case "drawing-physical": workspace.UnlinkedDrawings["one"] = "drawing"; break;
                case "drawing-unknown": workspace.UnlinkedDrawings["unknown"] = "drawing"; break;
                case "pin-unknown": workspace.Hardware["unknown"] = workspace.Hardware["one"]; break;
                case "pin-null": workspace.Hardware["one"] = null!; break;
                case "device-pin-null": workspace.Hardware["one"] = new("drawing", null!); break;
                case "physical-empty": workspace.Hardware["one"] = new("drawing", new(Guid.Empty, "One", true)); break;
                default: workspace.Hardware["one"] = workspace.Hardware["one"] with { HardwareId = " " }; break;
            }
            return workspace;
        }
        #endregion END private helpers
    }
}
