using Heimdall.Application;
using Heimdall.Domain.MultiSim;
using Heimdall.WarThunder;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Protects the game driver's translation rules independently of the legacy DCS projection and native range counts.</summary>
    public sealed class WarThunderInputLayoutTests
    {
        #region BEGIN Public
        /// <summary>An offset-fallback entry supplies the seventh native axis; the six ordinary controls keep their original slots.</summary>
        [Fact]
        public void Offset_fallback_throttle_shape_has_seven_axes_and_sixty_four_buttons()
        {
            var objects = WarThunderLayoutFixture.Throttle();
            var legacy = ControllerCapabilityMapper.Map(objects);
            Assert.Equal(6, legacy.Inputs.Count(input => input.Kind == ControllerInputKind.Axis));
            var layout = WarThunderInputLayout.Create(legacy.ReportedInputs);
            Assert.True(layout.AxesComplete); Assert.True(layout.ButtonsComplete);
            Assert.Equal(layout, layout with { });
            Assert.NotEqual(layout, layout with { AxesComplete = false });
            var slot = layout.Axes[0];
            Assert.Equal(slot, slot with { }); Assert.NotEqual(slot, slot with { NativeIndex = 1 });
            var trace = layout.Trace[0];
            Assert.Equal(trace, trace with { }); Assert.NotEqual(trace, trace with { NativeIndex = 999 });
            Assert.Equal(7, layout.Axes.Count); Assert.Equal(64, layout.Buttons.Count);
            string[] expected = ["JOY_Z", "JOY_Y", "JOY_X", "JOY_RY", "JOY_RX", "JOY_RZ", "JOY_SLIDER2"];
            Assert.Equal(expected, layout.Axes.Select(slot => Assert.Single(slot.StorageKeys)));
            Assert.Equal(Enumerable.Range(0, 7), layout.Axes.Select(slot => slot.NativeIndex));
            Assert.Contains(layout.Trace, item => item.NativeIndex == 6 && item.Detail.Contains("raw-offset fallback (28)", StringComparison.Ordinal));
            Assert.Equal(21, layout.Buttons.Single(slot => slot.StorageKeys.Contains("JOY_BTN22")).NativeIndex);
            Assert.NotEmpty(layout.Axes[6].InputId);
        }
        /// <summary>An unknown extra object at offset 36 is excluded while fallback slider storage and two POV axes remain represented.</summary>
        [Fact]
        public void Stick_shape_uses_slider_allocation_and_excludes_nonmatching_extra_axis()
        {
            var layout = WarThunderInputLayout.Create(ControllerInputCatalog.Create(WarThunderLayoutFixture.Stick()));
            Assert.True(layout.AxesComplete); Assert.True(layout.ButtonsComplete);
            Assert.Equal(10, layout.Axes.Count); Assert.Equal(25, layout.Buttons.Count);
            Assert.Equal("JOY_SLIDER1", Assert.Single(layout.Axes[0].StorageKeys));
            Assert.Equal("JOY_SLIDER2", Assert.Single(layout.Axes[1].StorageKeys));
            Assert.Equal("JOY_X", Assert.Single(layout.Axes[7].StorageKeys));
            Assert.Equal(8, layout.Axes.Single(slot => slot.StorageKeys.Contains("JOY_BTN_POV1_R")).NativeIndex);
            Assert.Equal(9, layout.Axes.Single(slot => slot.StorageKeys.Contains("JOY_BTN_POV1_U")).NativeIndex);
            Assert.True(layout.Axes[8].Virtual); Assert.True(layout.Buttons[21].Virtual);
            Assert.Contains(layout.Trace, item => item.NativeIndex is null && item.Detail.Contains("(36)", StringComparison.Ordinal));
        }
        /// <summary>Game button indices follow enumeration even when native instances are sparse and arrive in a different order.</summary>
        [Fact]
        public void Sparse_button_instances_do_not_become_game_ordinals()
        {
            var layout = WarThunderInputLayout.Create(ControllerInputCatalog.Create([
                WarThunderLayoutFixture.Button(8), WarThunderLayoutFixture.Button(2), WarThunderLayoutFixture.Hat(1)]));
            Assert.True(layout.ButtonsComplete);
            Assert.Equal("JOY_BTN9", Assert.Single(layout.Buttons[0].StorageKeys));
            Assert.Equal("JOY_BTN3", Assert.Single(layout.Buttons[1].StorageKeys));
            Assert.Equal("JOY_BTN_POV2_U", Assert.Single(layout.Buttons[2].StorageKeys));
            Assert.Equal(2, layout.Axes.Count); Assert.Equal(6, layout.Buttons.Count);
        }
        /// <summary>All supported raw-offset fallbacks are explicit, including zero; nonstandard offsets never consume a native slot.</summary>
        [Theory]
        [InlineData(0u, "JOY_X")]
        [InlineData(4u, "JOY_Y")]
        [InlineData(8u, "JOY_Z")]
        [InlineData(12u, "JOY_RX")]
        [InlineData(16u, "JOY_RY")]
        [InlineData(20u, "JOY_RZ")]
        [InlineData(24u, "JOY_SLIDER1")]
        [InlineData(28u, "JOY_SLIDER2")]
        public void Unknown_axis_guid_uses_only_driver_offset_fallbacks(uint offset, string key)
        {
            var layout = WarThunderInputLayout.Create(ControllerInputCatalog.Create([WarThunderLayoutFixture.Axis("a36d02f3", 0, offset)]));
            Assert.True(layout.AxesComplete); Assert.Equal(key, Assert.Single(Assert.Single(layout.Axes).StorageKeys));
        }
        /// <summary>GUID resolution wins over a conflicting raw offset and unusable offsets cannot be coerced into a known axis.</summary>
        [Fact]
        public void Guid_precedence_and_missing_evidence_are_not_inferred_from_counts()
        {
            var recognized = WarThunderLayoutFixture.Axis("a36d02e0", 0, 28);
            Assert.Equal("JOY_X", Assert.Single(Assert.Single(WarThunderInputLayout.Create(ControllerInputCatalog.Create([recognized])).Axes).StorageKeys));
            var unknown = WarThunderLayoutFixture.Axis("a36d02f3", 1, 29);
            var ignored = WarThunderInputLayout.Create(ControllerInputCatalog.Create([unknown]));
            Assert.Empty(ignored.Axes); Assert.True(ignored.AxesComplete);
            var incomplete = WarThunderInputLayout.Create(ControllerInputCatalog.Create([unknown with { Details = null }]));
            Assert.False(incomplete.AxesComplete); Assert.Contains(incomplete.Trace, item => item.Detail.Contains("Rescan", StringComparison.Ordinal));
            var slider = WarThunderLayoutFixture.Axis("a36d02e4", 2, 0) with { Details = null };
            Assert.False(WarThunderInputLayout.Create(ControllerInputCatalog.Create([slider])).AxesComplete);
            Assert.Throws<ArgumentNullException>(() => WarThunderInputLayout.Create(null!));
            Assert.Throws<InvalidDataException>(() => WarThunderInputLayout.Create(new(2, [], [])));
        }
        /// <summary>The driver rejects a third fallback slider but explicit duplicate slider storage remains represented as a separate native slot.</summary>
        [Fact]
        public void Slider_allocation_distinguishes_exhausted_fallback_from_duplicate_storage()
        {
            var one = WarThunderLayoutFixture.Axis("a36d02e4", 0, 4);
            var two = WarThunderLayoutFixture.Axis("a36d02e4", 1, 8);
            var three = WarThunderLayoutFixture.Axis("a36d02e4", 2, 12);
            var layout = WarThunderInputLayout.Create(ControllerInputCatalog.Create([one, two, three]));
            Assert.True(layout.AxesComplete); Assert.Equal(2, layout.Axes.Count);
            Assert.Contains(layout.Trace, item => item.Detail.Contains("no free", StringComparison.Ordinal));
            var explicitSlot = WarThunderLayoutFixture.Axis("a36d02e4", 3, 28);
            var duplicate = WarThunderInputLayout.Create(ControllerInputCatalog.Create([one, two, explicitSlot]));
            Assert.True(duplicate.AxesComplete); Assert.Equal(3, duplicate.Axes.Count);
            var secondFirst = WarThunderInputLayout.Create(ControllerInputCatalog.Create([explicitSlot, one]));
            Assert.Equal("JOY_SLIDER2", Assert.Single(secondFirst.Axes[0].StorageKeys));
            Assert.Equal("JOY_SLIDER1", Assert.Single(secondFirst.Axes[1].StorageKeys));
        }
        /// <summary>Duplicate identities remain ambiguous; native relative axes retain their slots without proving drawing support.</summary>
        [Fact]
        public void Ambiguity_and_relative_axes_block_only_the_affected_group()
        {
            var axis = WarThunderLayoutFixture.Axis("a36d02e0", 0, 0);
            var button = WarThunderLayoutFixture.Button(0);
            var duplicateAxis = WarThunderInputLayout.Create(ControllerInputCatalog.Create([axis, axis, button]));
            Assert.False(duplicateAxis.AxesComplete); Assert.True(duplicateAxis.ButtonsComplete);
            var relative = WarThunderInputLayout.Create(ControllerInputCatalog.Create([axis with { NativeType = 1 }, button]));
            Assert.True(relative.AxesComplete); Assert.Single(relative.Axes); Assert.True(relative.ButtonsComplete);
            var duplicateButton = WarThunderInputLayout.Create(ControllerInputCatalog.Create([axis, button, button]));
            Assert.True(duplicateButton.AxesComplete); Assert.False(duplicateButton.ButtonsComplete);
            var hat = WarThunderLayoutFixture.Hat(0);
            var duplicateHat = WarThunderInputLayout.Create(ControllerInputCatalog.Create([hat, hat]));
            Assert.False(duplicateHat.AxesComplete); Assert.False(duplicateHat.ButtonsComplete);
        }
        /// <summary>Driver limits and non-input objects are traced rather than silently converted to controls.</summary>
        [Fact]
        public void Driver_button_and_pov_limits_are_applied_before_range_count_checks()
        {
            var objects = Enumerable.Range(0, 497).Select(WarThunderLayoutFixture.Button).ToList();
            objects.AddRange(Enumerable.Range(0, 5).Select(WarThunderLayoutFixture.Hat));
            objects.Add(new(Guid.NewGuid(), 64, "Collection"));
            var layout = WarThunderInputLayout.Create(ControllerInputCatalog.Create(objects));
            Assert.Equal(512, layout.Buttons.Count); Assert.Equal(8, layout.Axes.Count);
            Assert.True(layout.AxesComplete); Assert.True(layout.ButtonsComplete);
            Assert.Contains(layout.Trace, item => item.Detail.Contains("button limit", StringComparison.Ordinal));
            Assert.Contains(layout.Trace, item => item.Detail.Contains("POV limit", StringComparison.Ordinal));
            Assert.Contains(layout.Trace, item => item.Kind == "Other" && item.NativeIndex is null);
        }
        /// <summary>The physical assignment pipeline consumes neutral evidence, accounts for the fallback slot and preserves native tuning.</summary>
        [Fact]
        public void Native_layout_drives_assignment_without_legacy_supported_inputs()
        {
            var backend = new WarThunderProfileBackend();
            var source = WarThunderBackendFixture.Source.Replace("axesCount:i=2", "axesCount:i=7", StringComparison.Ordinal)
                .Replace("axesOffset:i=2", "axesOffset:i=7", StringComparison.Ordinal)
                .Replace("buttonsCount:i=4", "buttonsCount:i=64", StringComparison.Ordinal)
                .Replace("buttonsOffset:i=4", "buttonsOffset:i=64", StringComparison.Ordinal);
            var profile = WarThunderBackendFixture.Import(backend, source);
            var capabilities = ControllerCapabilityMapper.Map(WarThunderLayoutFixture.Throttle());
            var observation = new ControllerObservation(new(Guid.NewGuid(), "Synthetic throttle", true,
                ProductGuid: Guid.Parse("00020001-0000-0000-0000-504944564944")), capabilities);
            var map = WarThunderPhysicalMap.Create(profile, "war-thunder.joystick.0", observation);
            Assert.Equal(map, map with { });
            Assert.NotNull(map.Layout); Assert.Equal(7, map.Layout.Axes.Count);
            Assert.DoesNotContain(map.Inputs, candidate => candidate.Key == "JOY_SLIDER2");
            Assert.DoesNotContain("mapping blocked", map.Message, StringComparison.Ordinal);
            var neutralOnly = WarThunderPhysicalMap.Create(profile, "war-thunder.joystick.0",
                observation with { Capabilities = capabilities with { Inputs = [], Unsupported = [] } });
            Assert.Equal(map.Inputs, neutralOnly.Inputs);
            var throttle = backend.GetCatalog(profile).Commands.Single(command => command.Metadata["war-thunder.command"] == "throttle");
            var input = map.Inputs.Single(candidate => candidate.Key == "JOY_Z");
            Assert.Equal(0, input.Index);
            var changed = WarThunderDrawingProjection.Assignment(profile, throttle.Id, "war-thunder.joystick.0", input).Apply(profile);
            Assert.Contains("throttle{axisId:i=0;nonlinearity:i=1;}", WarThunderBackendFixture.Export(backend, changed), StringComparison.Ordinal);
            Assert.Equal("throttle", Assert.Single(WarThunderDrawingProjection.Assignments(changed, "war-thunder.joystick.0", "JOY_Z", map)).Binding.Command);
            var mismatched = WarThunderBackendFixture.Import(backend, source.Replace("axesCount:i=7", "axesCount:i=6", StringComparison.Ordinal));
            var blocked = WarThunderPhysicalMap.Create(mismatched, "war-thunder.joystick.0", observation);
            Assert.DoesNotContain(blocked.Inputs, candidate => candidate.Type == CommandType.Axis);
            Assert.Equal(64, blocked.Inputs.Count); Assert.Contains("BLK declares 6", blocked.Message, StringComparison.Ordinal);
            var overlapped = WarThunderBackendFixture.Import(backend, source.Replace("axesOffset:i=7", "axesOffset:i=0", StringComparison.Ordinal));
            var ambiguous = WarThunderPhysicalMap.Create(overlapped, "war-thunder.joystick.0", observation);
            Assert.Empty(ambiguous.Inputs); Assert.NotNull(ambiguous.Layout); Assert.Contains("overlap", ambiguous.Message, StringComparison.Ordinal);
            Assert.Equal(source, WarThunderBackendFixture.Export(backend, profile));
        }
        /// <summary>Drawing aliases follow their reported object even when the driver's slider storage assignment has the opposite order.</summary>
        [Fact]
        public void Drawing_bridge_does_not_confuse_driver_storage_with_physical_input_alias()
        {
            var objects = new[] { WarThunderLayoutFixture.Axis("a36d02e4", 6, 24), WarThunderLayoutFixture.Axis("a36d02e4", 2, 28) }
                .Concat(Enumerable.Range(0, 4).Select(WarThunderLayoutFixture.Button)).ToArray();
            var observation = new ControllerObservation(new(Guid.NewGuid(), "Synthetic slider bridge", true,
                ProductGuid: Guid.Parse("00020001-0000-0000-0000-504944564944")), ControllerCapabilityMapper.Map(objects));
            var map = WarThunderPhysicalMap.Create(WarThunderBackendFixture.Import(new()), "war-thunder.joystick.0", observation);
            Assert.NotNull(map.Layout); Assert.True(map.Layout.AxesComplete);
            Assert.Equal("JOY_SLIDER1", Assert.Single(map.Layout.Axes[0].StorageKeys));
            Assert.Contains(new WarThunderPhysicalInput("JOY_SLIDER2", CommandType.Axis, 0), map.Inputs);
            Assert.Contains(new WarThunderPhysicalInput("JOY_SLIDER1", CommandType.Axis, 1), map.Inputs);
            var hat = WarThunderLayoutFixture.Hat(4);
            var unavailable = WarThunderPhysicalMap.Create(WarThunderBackendFixture.Import(new()), "war-thunder.joystick.0",
                observation with { Capabilities = ControllerCapabilityMapper.Map([hat]) });
            Assert.NotNull(unavailable.Layout); Assert.Equal(2, unavailable.Layout.Axes.Count); Assert.Equal(4, unavailable.Layout.Buttons.Count);
            Assert.Empty(unavailable.Inputs);
        }
        #endregion END Public
    }
}
