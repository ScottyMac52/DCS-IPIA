using Heimdall.Application;

namespace Heimdall.WarThunder
{
    /// <summary>Implements the published ordinary DirectInput driver's GUID-first, raw-offset and slider allocation rules.</summary>
    internal sealed class WarThunderAxisLayout
    {
        private static readonly Guid SliderGuid = new("a36d02e4-c9f3-11cf-bfc7-444553540000");
        private static readonly string[] OffsetKeys = ["JOY_X", "JOY_Y", "JOY_Z", "JOY_RX", "JOY_RY", "JOY_RZ", "JOY_SLIDER1", "JOY_SLIDER2"];
        private readonly HashSet<string> _sliders = new(StringComparer.Ordinal);
        #region BEGIN Public
        internal WarThunderAxisResolution Resolve(ReportedControllerInput input)
        {
            var item = input.Evidence;
            var key = GuidStorageKey(item.ObjectGuid);
            var result = key is not null ? new WarThunderAxisResolution(key, "Standard axis GUID takes precedence over the raw offset.", true)
                : item.ObjectGuid == SliderGuid ? Slider(item.Details) : Fallback(item.Details);
            if (result.StorageKey is "JOY_SLIDER1" or "JOY_SLIDER2") _sliders.Add(result.StorageKey);
            if (input.Ambiguous) return result with { Complete = false, Detail = "Duplicate reported object identity is unsupported or ambiguous." };
            if (result.StorageKey is not null && input.Relative != false)
                return result with { Detail = result.Detail + " Native relative/unspecified axis is counted; drawing support is a separate compatibility projection." };
            return result;
        }
        #endregion END Public
        #region BEGIN private helpers
        private static string? GuidStorageKey(Guid guid)
        {
            var text = guid.ToString("D");
            if (!text.EndsWith("-c9f3-11cf-bfc7-444553540000", StringComparison.Ordinal)) return null;
            return text[..8] switch
            {
                "a36d02e0" => "JOY_X", "a36d02e1" => "JOY_Y", "a36d02e2" => "JOY_Z",
                "a36d02f4" => "JOY_RX", "a36d02f5" => "JOY_RY", "a36d02e3" => "JOY_RZ", _ => null
            };
        }
        private WarThunderAxisResolution Slider(ControllerObjectDetails? details)
        {
            if (details is null) return new(null, "Slider raw offset is missing. Rescan the controller.", false);
            if (details.NativeOffset is 24 or 28) return new(OffsetKeys[(int)details.NativeOffset / 4], "Slider GUID with explicit driver slider offset.", true);
            var key = !_sliders.Contains("JOY_SLIDER1") ? "JOY_SLIDER1" : !_sliders.Contains("JOY_SLIDER2") ? "JOY_SLIDER2" : null;
            return key is null ? new(null, "Excluded: no free fallback slider storage remains.", true)
                : new(key, "Slider GUID with a nonstandard raw offset uses the first unused slider storage.", true);
        }
        private static WarThunderAxisResolution Fallback(ControllerObjectDetails? details)
        {
            if (details is null) return new(null, "Unknown axis GUID lacks raw-offset evidence. Rescan the controller.", false);
            var offset = details.NativeOffset;
            return offset <= 28 && offset % 4 == 0
                ? new(OffsetKeys[(int)offset / 4], "Unknown GUID accepted by the driver's raw-offset fallback (" + offset + ").", true)
                : new(null, "Excluded: neither GUID nor raw offset (" + offset + ") is recognized by the driver.", true);
        }
        #endregion END private helpers
    }
}
