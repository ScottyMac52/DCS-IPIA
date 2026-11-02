using System.Security.Cryptography;
using System.Text;
using System.Globalization;

namespace Heimdall.Application
{
    /// <summary>Interprets actual native objects once for discovery, inventory and live testing.</summary>
    public static class ControllerCapabilityMapper
    {
        private const string AxisSuffix = "-c9f3-11cf-bfc7-444553540000";
        #region BEGIN Public
        /// <summary>Maps supported objects; duplicates and unsupported controls remain explicit diagnostics.</summary>
        /// <param name="objects">Complete native object enumeration.</param>
        /// <returns>Deterministically ordered supported controls and diagnostics.</returns>
        public static ControllerCapabilities Map(IEnumerable<ControllerObject> objects)
        {
            var supported = new List<ControllerInput>();
            var rejected = new List<UnsupportedControllerObject>();
            var evidence = objects.ToArray();
            var repeated = evidence.GroupBy(x => (x.ObjectGuid, Type: x.NativeType & 0x00ffffff))
                .Where(x => x.Count() > 1).Select(x => x.Key).ToHashSet();
            var slider = 0;
            foreach (var item in evidence.OrderBy(x => (x.NativeType >> 8) & 65535).ThenBy(x => x.NativeType).ThenBy(x => x.ObjectGuid))
            {
                if (repeated.Contains((item.ObjectGuid, item.NativeType & 0x00ffffff)))
                {
                    rejected.Add(new(item, "Duplicate native object identity; no mapping was guessed."));
                    continue;
                }
                var instance = (int)((item.NativeType >> 8) & 65535);
                var kind = item.NativeType & 255;
                if ((kind & 12) != 0 && kind is 4 or 8 or 12 && instance < 128)
                    supported.Add(new(item, ControllerInputKind.Button, "JOY_BTN" + (instance + 1).ToString(CultureInfo.InvariantCulture), 48 + instance));
                else if (kind == 16 && instance < 4)
                    supported.Add(new(item, ControllerInputKind.Hat, "JOY_BTN_POV" + (instance + 1).ToString(CultureInfo.InvariantCulture), 32 + instance * 4));
                else if (kind == 2)
                {
                    var guid = item.ObjectGuid.ToString("D");
                    var axis = guid.EndsWith(AxisSuffix, StringComparison.Ordinal) ? guid[..8] switch
                    {
                        "a36d02e0" => (0, "JOY_X"),
                        "a36d02e1" => (4, "JOY_Y"),
                        "a36d02e2" => (8, "JOY_Z"),
                        "a36d02f4" => (12, "JOY_RX"),
                        "a36d02f5" => (16, "JOY_RY"),
                        "a36d02e3" => (20, "JOY_RZ"),
                        "a36d02e4" when slider < 2 => (24 + slider * 4, "JOY_SLIDER" + (++slider).ToString(CultureInfo.InvariantCulture)),
                        _ => (-1, "")
                    } : (-1, "");
                    if (axis.Item1 >= 0) supported.Add(new(item, ControllerInputKind.Axis, axis.Item2, axis.Item1));
                    else rejected.Add(new(item, "Unknown or excess absolute axis; no DCS mapping was invented."));
                }
                else rejected.Add(new(item, "Unsupported object type or instance limit. Relative axes, collections and non-input objects are not assignable."));
            }
            var duplicates = supported.GroupBy(x => x.DcsKey, StringComparer.Ordinal).Where(x => x.Count() > 1)
                .Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
            foreach (var input in supported.Where(x => duplicates.Contains(x.DcsKey)))
                rejected.Add(new(input.Evidence, "Ambiguous duplicate DCS identity: " + input.DcsKey));
            return new(1, supported.Where(x => !duplicates.Contains(x.DcsKey)).ToArray(), rejected.ToArray()) { NativeObjects = Array.AsReadOnly(evidence) };
        }
        /// <summary>Gets a content identity for controls, independent of localized object labels and scan order.</summary>
        /// <param name="capabilities">Versioned capability evidence.</param>
        /// <returns>Stable reusable definition ID.</returns>
        public static string Fingerprint(ControllerCapabilities capabilities)
        {
            if (capabilities.SchemaVersion != 1) throw new InvalidDataException("Unsupported controller capability version.");
            var parts = capabilities.Inputs.Select(x => x.Evidence.ObjectGuid.ToString("D") + ":" +
                x.Evidence.NativeType.ToString(CultureInfo.InvariantCulture) + ":" + x.DcsKey + ":" + x.Kind + ":" + x.Offset.ToString(CultureInfo.InvariantCulture))
                .Concat(capabilities.Unsupported.Select(x => "unsupported:" + x.Evidence.ObjectGuid.ToString("D") + ":" +
                    x.Evidence.NativeType.ToString(CultureInfo.InvariantCulture)))
                .Order(StringComparer.Ordinal);
            return "controls-v1-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", parts))));
        }
        #endregion END Public
    }
}
