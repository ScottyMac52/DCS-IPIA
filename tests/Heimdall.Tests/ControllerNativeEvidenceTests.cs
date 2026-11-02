using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Heimdall.Application;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Protects complete native evidence independently of DCS aliases, game ranges and live state formats.</summary>
    public sealed class ControllerNativeEvidenceTests
    {
        #region BEGIN Public
        /// <summary>Documented field offsets retain raw device metadata and stop at the declared structure boundary.</summary>
        [Theory]
        [InlineData(552)]
        [InlineData(554)]
        [InlineData(556)]
        [InlineData(558)]
        [InlineData(560)]
        [InlineData(562)]
        [InlineData(564)]
        [InlineData(566)]
        [InlineData(568)]
        [InlineData(572)]
        [InlineData(574)]
        [InlineData(576)]
        public void Native_reader_obeys_documented_byte_layout(int size)
        {
            var bytes = NativeBytes(size);
            var input = ControllerObjectReader.Read(bytes);
            Assert.Equal(Guid.Parse("a36d02e1-c9f3-11cf-bfc7-444553540000"), input.ObjectGuid);
            Assert.Equal(258u, input.NativeType); Assert.Equal("Native Y", input.Name);
            var details = Assert.IsType<ControllerObjectDetails>(input.Details);
            Assert.Equal(104u, details.NativeOffset); Assert.Equal(0x100u, details.Flags);
            Assert.Equal(size >= 556 ? 12u : (uint?)null, details.MaximumForce);
            Assert.Equal(size >= 560 ? 13u : (uint?)null, details.ForceResolution);
            Assert.Equal(size >= 562 ? (ushort)2 : (ushort?)null, details.CollectionNumber);
            Assert.Equal(size >= 564 ? (ushort)3 : (ushort?)null, details.DesignatorIndex);
            Assert.Equal(size >= 566 ? (ushort)1 : (ushort?)null, details.UsagePage);
            Assert.Equal(size >= 568 ? (ushort)49 : (ushort?)null, details.Usage);
            Assert.Equal(size >= 572 ? 17u : (uint?)null, details.Dimension);
            Assert.Equal(size >= 574 ? (ushort)4 : (ushort?)null, details.Exponent);
            Assert.Equal(size >= 576 ? (ushort)5 : (ushort?)null, details.ReportId);
        }
        /// <summary>Truncated or malformed native structures cannot become apparently valid object evidence.</summary>
        [Theory]
        [InlineData(0, 0)]
        [InlineData(3, 0)]
        [InlineData(552, 551)]
        [InlineData(552, 576)]
        public void Invalid_structure_sizes_are_rejected(int length, int declared)
        {
            var bytes = new byte[length];
            if (length >= 4) BinaryPrimitives.WriteUInt32LittleEndian(bytes, (uint)declared);
            Assert.Throws<InvalidDataException>(() => ControllerObjectReader.Read(bytes));
        }
        /// <summary>Names may occupy their entire native field; unknown trailing bytes do not become extra fields.</summary>
        [Fact]
        public void Full_length_name_and_trailing_native_bytes_are_retained_safely()
        {
            var bytes = NativeBytes(580);
            Encoding.Unicode.GetBytes(new string('Z', 260)).CopyTo(bytes, 32);
            Assert.Equal(new string('Z', 260), ControllerObjectReader.Read(bytes).Name);
        }
        /// <summary>Unknown, relative, sparse and duplicate objects retain their original positions and explicit ambiguity.</summary>
        [Fact]
        public void Neutral_catalog_does_not_filter_sort_or_relabel_native_inputs()
        {
            ControllerObject[] objects = [new(Guid.NewGuid(), 2, "Unknown absolute axis") { Details = new(24, 0) },
                new(Guid.NewGuid(), 1, "Relative axis") { Details = new(600, 0x200) },
                new(Guid.NewGuid(), 4u | (200u << 8), "Button 201") { Details = new(800, 0) },
                new(Guid.NewGuid(), 16, "Hat") { Details = new(820, 0) },
                new(Guid.NewGuid(), 64, "Collection") { Details = new(900, 0) }];
            var legacy = ControllerCapabilityMapper.Map(objects);
            var catalog = legacy.PhysicalInputs;
            Assert.True(catalog.HasNativeDetails); Assert.Empty(catalog.Diagnostics);
            Assert.Equal(objects, catalog.Objects.Select(input => input.Evidence));
            Assert.Equal(Enumerable.Range(0, objects.Length), catalog.Objects.Select(input => input.EnumerationIndex));
            Assert.Equal(PhysicalInputKind.Axis, catalog.Objects[0].Kind); Assert.False(catalog.Objects[0].Relative);
            Assert.True(catalog.Objects[1].Relative); Assert.Null(catalog.Objects[2].Relative);
            Assert.Equal(PhysicalInputKind.Button, catalog.Objects[2].Kind); Assert.Equal(200, catalog.Objects[2].Instance);
            Assert.Equal(PhysicalInputKind.Hat, catalog.Objects[3].Kind); Assert.Equal(PhysicalInputKind.Other, catalog.Objects[4].Kind);
            Assert.DoesNotContain(legacy.Inputs, input => input.Evidence == objects[0]);
            var reordered = ControllerInputCatalog.Create(objects.Reverse().Select(item => item with { Name = "Localized", Details = item.Details! with { NativeOffset = 42 } }).ToArray());
            Assert.Equal(catalog.Objects.Select(item => item.Id).Order(StringComparer.Ordinal), reordered.Objects.Select(item => item.Id).Order(StringComparer.Ordinal));
            var duplicate = ControllerInputCatalog.Create([objects[0], objects[0]]);
            Assert.Equal(2, duplicate.Objects.Count); Assert.All(duplicate.Objects, input => Assert.True(input.Ambiguous));
            Assert.Single(duplicate.Diagnostics);
        }
        /// <summary>Legacy captures remain readable and preserve their DCS fingerprint without pretending raw metadata exists.</summary>
        [Fact]
        public void Legacy_json_and_fingerprint_compatibility_are_explicit()
        {
            var legacy = JsonSerializer.Deserialize<ControllerObject>("{\"ObjectGuid\":\"a36d02e1-c9f3-11cf-bfc7-444553540000\",\"NativeType\":258,\"Name\":\"Y\"}")!;
            Assert.Null(legacy.Details);
            var before = ControllerCapabilityMapper.Map([legacy]);
            Assert.False(before.PhysicalInputs.HasNativeDetails); Assert.Single(before.PhysicalInputs.Diagnostics);
            var fresh = ControllerCapabilityMapper.Map([legacy with { Details = new(100, 0x100, UsagePage: 1, Usage: 49) }]);
            Assert.Equal(ControllerCapabilityMapper.Fingerprint(before), ControllerCapabilityMapper.Fingerprint(fresh));
            Assert.DoesNotContain("PhysicalInputs", JsonSerializer.Serialize(before), StringComparison.Ordinal);
            Assert.False(ControllerInputCatalog.Create(null).HasNativeDetails);
            Assert.False(ControllerInputCatalog.Create([]).HasNativeDetails);
            Assert.Single(ControllerInputCatalog.Create(null).Diagnostics); Assert.Single(ControllerInputCatalog.Create([]).Diagnostics);
        }
        /// <summary>Reports expose neutral and compatibility views together without losing failures on another device.</summary>
        [Fact]
        public void Discovery_report_is_independent_of_profiles_and_native_game_files()
        {
            var one = new DetectedDcsDevice(Guid.NewGuid(), "One", true);
            var objectEvidence = ControllerObjectReader.Read(NativeBytes(576));
            var observations = new ControllerObservation[] { new(one, ControllerCapabilityMapper.Map([objectEvidence])),
                new(one with { PhysicalGuid = Guid.NewGuid() }, null, "Read failed") };
            var before = JsonSerializer.Serialize(observations);
            using var json = JsonDocument.Parse(ControllerEvidenceReport.Create(observations, DateTimeOffset.UnixEpoch));
            Assert.Equal(1, json.RootElement.GetProperty("schemaVersion").GetInt32());
            var devices = json.RootElement.GetProperty("devices");
            Assert.Equal(2, devices.GetArrayLength());
            var input = devices[0].GetProperty("physicalInputs").GetProperty("objects")[0];
            Assert.Equal("Axis", input.GetProperty("kind").GetString());
            Assert.Equal(104u, input.GetProperty("evidence").GetProperty("details").GetProperty("nativeOffset").GetUInt32());
            Assert.Equal("Read failed", devices[1].GetProperty("error").GetString());
            Assert.False(devices[1].GetProperty("physicalInputs").GetProperty("hasNativeDetails").GetBoolean());
            Assert.Equal(before, JsonSerializer.Serialize(observations));
            Assert.Throws<ArgumentNullException>(() => ControllerEvidenceReport.Create(null!, DateTimeOffset.UnixEpoch));
        }
        /// <summary>Native metadata has value equality across serialization and independent captures.</summary>
        [Fact]
        public void Metadata_value_identity_round_trips_without_reference_equality()
        {
            var details = new ControllerObjectDetails(24, 256, 12, 13, 2, 3, 1, 54, 17, 4, 5);
            var copy = JsonSerializer.Deserialize<ControllerObjectDetails>(JsonSerializer.Serialize(details))!;
            Assert.NotSame(details, copy); Assert.Equal(details, copy);
            Assert.True(details.Equals((object)copy)); Assert.False(details.Equals(new object()));
            Assert.True(details == copy); Assert.False(details != copy);
            Assert.NotEqual(details, copy with { Usage = 55 });
            Assert.Equal(details.GetHashCode(), copy.GetHashCode());
            Assert.Contains("NativeOffset = 24", details.ToString(), StringComparison.Ordinal);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static byte[] NativeBytes(int size)
        {
            var bytes = new byte[Math.Max(580, size)];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, (uint)size);
            Assert.True(Guid.Parse("a36d02e1-c9f3-11cf-bfc7-444553540000").TryWriteBytes(bytes.AsSpan(4, 16)));
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(20), 104);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(24), 258);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(28), 0x100);
            Encoding.Unicode.GetBytes("Native Y\0ignored").CopyTo(bytes, 32);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(552), 12);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(556), 13);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(560), 2);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(562), 3);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(564), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(566), 49);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(568), 17);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(572), 4);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(574), 5);
            return bytes;
        }
        #endregion END private helpers
    }
}
