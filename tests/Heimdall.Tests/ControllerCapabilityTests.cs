using System.Buffers.Binary;
using Heimdall.Application;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Verifies sparse native enumeration maps exactly to the live-test decoder.</summary>
    public sealed class ControllerCapabilityTests
    {
        #region BEGIN Public
        /// <summary>Every supported axis, sparse button and POV uses the same state offsets as live testing.</summary>
        [Fact]
        public void Enumerated_controls_match_live_decoding_and_stable_fingerprints()
        {
            var objects = new[]
            {
                Axis("a36d02e0",0), Axis("a36d02e1",1), Axis("a36d02e2",2), Axis("a36d02f4",3),
                Axis("a36d02f5",4), Axis("a36d02e3",5), Axis("a36d02e4",6), Axis("a36d02e4",7),
                new ControllerObject(Guid.NewGuid(), 12u | (127u << 8), "button 128"),
                new ControllerObject(Guid.NewGuid(), 16u | (3u << 8), "hat 4")
            };
            var caps = ControllerCapabilityMapper.Map(objects);
            Assert.Empty(caps.Unsupported);
            Assert.Equal(10, caps.Inputs.Count);
            Assert.Equal(17, caps.DcsKeys.Count);
            Assert.DoesNotContain("JOY_BTN1", caps.DcsKeys);
            Assert.Contains("JOY_BTN128", caps.DcsKeys);
            Assert.Contains("JOY_BTN_POV4_UL", caps.DcsKeys);
            var state = new byte[176];
            state[175] = 128;
            BinaryPrimitives.WriteInt32LittleEndian(state.AsSpan(0,4),65535);
            BinaryPrimitives.WriteInt32LittleEndian(state.AsSpan(44,4),9000);
            var decoded = DirectInputStateDecoder.Decode(state, caps.Inputs.ToDictionary(x => x.DcsKey, x => x.Offset));
            Assert.Equal(1, decoded["JOY_BTN128"]);
            Assert.Equal(1, decoded["JOY_X"]);
            Assert.Equal(1, decoded["JOY_BTN_POV4_R"]);
            Assert.Equal(0, decoded["JOY_BTN_POV4_U"]);
            var reordered = ControllerCapabilityMapper.Map(objects.Reverse().Select(x => x with { Name = "localized" }));
            Assert.Equal(ControllerCapabilityMapper.Fingerprint(caps), ControllerCapabilityMapper.Fingerprint(reordered));
            Assert.Throws<InvalidDataException>(() => ControllerCapabilityMapper.Fingerprint(caps with { SchemaVersion = 2 }));
        }

        /// <summary>Unsupported relative axes, limits, unknown GUIDs and ambiguous duplicates are explicit.</summary>
        [Fact]
        public void Unsupported_objects_are_reported_without_invented_controls()
        {
            var duplicate = Axis("a36d02e4",0);
            var caps = ControllerCapabilityMapper.Map([
                duplicate, duplicate, Axis("a36d02e4",1), Axis("a36d02e4",2), Axis("a36d02e4",3),
                Axis("a36d02e0",4), Axis("a36d02e0",5),
                new(Guid.NewGuid(),2u | (6u << 8),"unknown axis"),
                new(Guid.NewGuid(),1u | (7u << 8),"relative axis"),
                new(Guid.NewGuid(),12u | (128u << 8),"excess button"),
                new(Guid.NewGuid(),16u | (4u << 8),"excess hat"),
                new(Guid.NewGuid(),128u,"collection")
            ]);
            Assert.Equal(2, caps.Inputs.Count);
            Assert.Equal(10, caps.Unsupported.Count);
            Assert.DoesNotContain("JOY_X",caps.DcsKeys);
            Assert.Contains(caps.Unsupported,x => x.Reason.Contains("Duplicate native",StringComparison.Ordinal));
            Assert.Contains(caps.Unsupported,x => x.Reason.Contains("Ambiguous duplicate DCS",StringComparison.Ordinal));
            var empty = ControllerCapabilityMapper.Map([]);
            Assert.Empty(empty.Inputs);
            Assert.Empty(empty.DcsKeys);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static ControllerObject Axis(string prefix, int instance) =>
            new(new Guid(prefix + "-c9f3-11cf-bfc7-444553540000"),2u | ((uint)instance << 8),prefix);
        #endregion END private helpers
    }
}
