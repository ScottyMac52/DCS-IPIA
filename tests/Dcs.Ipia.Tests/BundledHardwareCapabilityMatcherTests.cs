using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Requires native recognition and exact controls before offering artwork candidates.</summary>
    public sealed class BundledHardwareCapabilityMatcherTests
    {
        private static readonly string[] ExpectedCandidates = ["one", "two"];
        #region BEGIN Public
        /// <summary>Compatible repeated units remain candidates; ambiguous artwork never selects a grip.</summary>
        [Fact]
        public void Exact_controls_and_recognition_preserve_ambiguity()
        {
            var caps = ControllerCapabilityMapper.Map([new(Guid.NewGuid(),12,"Button")]);
            var observed = new ControllerObservation(new(Guid.NewGuid(),"Native panel",true),caps);
            var hardware = new[]
            {
                Hardware("one","JOY_BTN1"), Hardware("two","JOY_BTN1"), Hardware("wrong-controls","JOY_BTN2")
            };
            var map = new SharedDeviceMap([
                new("one",["panel"],null,null),new("two",["panel"],null,null),new("wrong-controls",["panel"],null,null)
            ],new Dictionary<string,IReadOnlyDictionary<string,string>>(),[]);
            var sources = new Dictionary<string,string>
            {
                ["config/ipia-hardware.json"] = JsonSerializer.Serialize(hardware),
                ["config/ipia-device-map.json"] = JsonSerializer.Serialize(map)
            };
            Assert.Equal(ExpectedCandidates,BundledHardwareCapabilityMatcher.Candidates(sources,observed));
            Assert.Empty(BundledHardwareCapabilityMatcher.Candidates(sources,observed with { Device = observed.Device with { Name = "Unknown controller" } }));
            Assert.Empty(BundledHardwareCapabilityMatcher.Candidates(sources,observed with { Capabilities = null }));
            Assert.Empty(BundledHardwareCapabilityMatcher.Candidates(sources,observed with { Capabilities = caps with { Unsupported = [new(new(Guid.NewGuid(),1,"relative"),"unsupported")] } }));
            Assert.Empty(BundledHardwareCapabilityMatcher.Candidates(new Dictionary<string,string>(),observed));
            sources.Remove("config/ipia-device-map.json");
            Assert.Equal(ExpectedCandidates,BundledHardwareCapabilityMatcher.Candidates(sources,observed));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static WorkspaceHardware Hardware(string id,string key) => new(id,id,
            "Native panel {" + Guid.NewGuid().ToString("D") + "}.diff.lua",
            new(id,null,"path.svg",100,100,[],[]) { Controls = [new("control","Button",key,0,0,10,10)] });
        #endregion END private helpers
    }
}
