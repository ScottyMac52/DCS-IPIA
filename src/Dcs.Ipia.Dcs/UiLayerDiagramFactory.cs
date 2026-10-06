using System.Globalization;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Dcs
{
    /// <summary>Builds module device diagrams from DCS-Common hardware graphics and optional UI Layer templates.</summary>
    public static class UiLayerDiagramFactory
    {
        #region BEGIN Public
        /// <summary>Builds diagrams for module-used devices and instances using committed SVG text.</summary>
        /// <param name="import">Validated module import.</param>
        /// <param name="catalog">Validated DCS-Common catalog.</param>
        /// <param name="sources">Committed shared SVG texts keyed by repository path.</param>
        /// <param name="shared">Validated physical-to-logical control mappings.</param>
        /// <param name="onGraphicError">Optional per-device diagnostic receiver; without one, invalid graphics fail the import projection.</param>
        /// <returns>Authored diagrams and their mapped controls for each supported profile.</returns>
        public static IReadOnlyList<UiLayerDiagram> Create(ConsumerImportResult import,
                                                           SharedCatalogImportResult catalog,
                                                           IReadOnlyDictionary<string, string> sources,
                                                           SharedLuaImportResult? shared = null,
                                                           Action<DeviceProfile, Exception>? onGraphicError = null)
        {
            ArgumentNullException.ThrowIfNull(import);
            ArgumentNullException.ThrowIfNull(catalog);
            ArgumentNullException.ThrowIfNull(sources);
            if (!import.Succeeded || !catalog.Succeeded)
                throw new ArgumentException("Validated imports are required.");
            var kneeboard = import.Metadata["config/kneeboard.json"];
            var usage = kneeboard.TryGetProperty("uiLayerUtilization", out var utilization)
                            ? utilization.GetProperty("bindings").EnumerateArray().ToArray()
                            : [];
            var diagrams = new List<UiLayerDiagram>();
            foreach (var profile in import.Configuration!.Profiles)
            {
                try
                {
                    diagrams.Add(CreateProfile(import, profile, usage, catalog, sources, shared));
                }
                catch (Exception ex)
                    when (onGraphicError is not null &&
                          ex is (InvalidDataException or FormatException or XmlException or ArgumentException or
                                    InvalidOperationException or KeyNotFoundException))
                {
                    onGraphicError!(profile, ex);
                }
            }
            return diagrams;
        }
        #endregion END Public
        #region BEGIN private helpers
        private static UiLayerDiagram CreateProfile(ConsumerImportResult import, DeviceProfile profile,
                                                    JsonElement[] usage, SharedCatalogImportResult catalog,
                                                    IReadOnlyDictionary<string, string> sources,
                                                    SharedLuaImportResult? shared)
        {

            var rows = ReadUsageRows(usage, profile);
            var device = catalog.Devices.Single(x => x.StableKey == profile.DeviceId ||
                                                     x.Aliases.Contains(profile.DeviceId, StringComparer.Ordinal));
            var template = catalog.Templates.SingleOrDefault(x => x.DeviceKey == device.StableKey);
            var projected = ReadProjectedBindings(import, profile, rows, shared);
            var supportsUi = ReadUiSupport(profile, template, rows, projected);
            var path = $"assets/shared/hardware/svg/{device.StableKey}.svg";
            if (!sources.TryGetValue(path, out var svg))
                throw new InvalidDataException($"Missing committed hardware image: {path}");
            var (root, width, height) = ReadCanvas(svg, path);
            var background = ReadBackground(root, width, height, path);
            var boxes = ReadBoxes(root, width, height, path);
            var mapped = (shared?.Controls ?? [])
                             .Where(x => x.Control.DeviceId == device.Id)
                             .ToDictionary(x => x.Control.StableKey, x => x, StringComparer.Ordinal);
            ValidateGraphicMappings(shared, boxes, mapped, path);
            var controls = ReadGraphicControls(boxes, mapped, path);
            var callouts = new List<UiLayerCallout>();
            AddExplicitCallouts(callouts, rows, template, boxes, device, catalog, import, profile, shared, controls, path);
            AddProjectedCallouts(callouts, projected, controls);
            var canonicalPath = ReadCanonicalPath(import, shared, profile);
            return new(device.DisplayName, profile.Instance, path, width, height, background,
                       callouts)
            {
                Svg = svg,
                ProfileId = profile.Id,
                Controls = controls,
                SupportsUiLayer = supportsUi,
                CanonicalUiLayerSourcePath = canonicalPath
            };
        }
        /// <summary>Selects explicit usage for one module device instance.</summary>
        private static JsonElement[] ReadUsageRows(JsonElement[] usage, DeviceProfile profile)
        {
            var rows =
                usage
                    .Where(x => x.GetProperty("deviceId").GetString() == profile.DeviceId &&
                                (x.TryGetProperty("deviceInstance", out var instance) ? instance.GetString() : null) ==
                                    profile.Instance)
                    .ToArray();
            return rows;
        }

        /// <summary>Projects shared bindings only when explicit module usage is absent.</summary>
        private static IReadOnlyList<DcsBinding> ReadProjectedBindings(ConsumerImportResult import, DeviceProfile profile,
                                                                       JsonElement[] rows, SharedLuaImportResult? shared)
        {
            IReadOnlyList<DcsBinding> projected = rows.Length == 0 && shared is not null &&
                !(import.Metadata["config/kneeboard.json"].TryGetProperty("uiLayerUtilization", out var explicitUsage) &&
                  explicitUsage.TryGetProperty("mode", out var usageMode) && usageMode.GetString() == "explicit")
                                                      ? ModuleUiLayerBindingImporter.Import(import, shared, profile)
                                                      : [];
            return projected;
        }

        /// <summary>Checks template instance eligibility and authored UI usage.</summary>
        private static bool ReadUiSupport(DeviceProfile profile, UiLayerTemplate? template, JsonElement[] rows,
                                           IReadOnlyList<DcsBinding> projected)
        {
            var supportsUi =
                template is not null && (template.Instances.Count == 0 ||
                                         template.Instances.Contains(profile.Instance, StringComparer.Ordinal)) ||
                projected.Count > 0;
            if (!supportsUi && rows.Length > 0)
                throw new InvalidDataException(
                    $"UI Layer template does not apply to {profile.DeviceId} {profile.Instance}.");
            return supportsUi;
        }

        /// <summary>Loads a bounded SVG document and validates its canvas.</summary>
        private static (XElement Root, double Width, double Height) ReadCanvas(string svg, string path)
        {
            using var reader = XmlReader.Create(new StringReader(svg), new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 4_000_000
            });
            var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            var root = document.Root ?? throw new InvalidDataException($"Empty SVG: {path}");
            var viewBox = (root.Attribute("viewBox")?.Value ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (viewBox.Length != 4 || viewBox[0] != "0" || viewBox[1] != "0")
                throw new InvalidDataException($"Unsupported SVG viewBox: {path}");
            var width = Number(viewBox[2]);
            var height = Number(viewBox[3]);
            if (Size(root, "width", path, "SVG canvas") != width ||
                Size(root, "height", path, "SVG canvas") != height)
                throw new InvalidDataException($"Hardware graphic {path} SVG canvas differs from its viewBox.");
            return (root, width, height);
        }

        /// <summary>Checks embedded image bounds and decodes the pinned background.</summary>
        private static byte[] ReadBackground(XElement root, double width, double height, string path)
        {
            var image = root.Descendants().FirstOrDefault(x => x.Name.LocalName == "image");
            if (image is null)
                throw new InvalidDataException($"Missing embedded hardware image: {path}");
            var imageX = Coordinate(image, "x", path, "embedded image");
            var imageY = Coordinate(image, "y", path, "embedded image");
            var imageWidth = Size(image, "width", path, "embedded image");
            var imageHeight = Size(image, "height", path, "embedded image");
            if (imageX > width || imageY > height || imageWidth > width - imageX ||
                imageHeight > height - imageY)
                throw new InvalidDataException($"Hardware graphic {path} embedded image extends outside its viewBox.");
            return DecodeBackground(image, path);
        }

        /// <summary>Locates authored callouts and validates their canvas bounds.</summary>
        private static IReadOnlyDictionary<string, XElement> ReadBoxes(XElement root, double width, double height, string path)
        {
            var boxes = SvgCalloutLocator.Find(root);
            foreach (var (control, box) in boxes)
            {
                var x = Coordinate(box, "x", path, control);
                var y = Coordinate(box, "y", path, control);
                var boxWidth = Size(box, "width", path, control);
                var boxHeight = Size(box, "height", path, control);
                if (x > width || y > height || boxWidth > width - x || boxHeight > height - y)
                    throw new InvalidDataException(
                        $"Hardware graphic {path} callout {control} extends outside its {width}x{height} viewBox.");
            }
            return boxes;
        }

        /// <summary>Rejects graphic identities missing from the shared Lua definition.</summary>
        private static void ValidateGraphicMappings(SharedLuaImportResult? shared, IReadOnlyDictionary<string, XElement> boxes,
                                                      Dictionary<string, SharedHardwareControl> mapped, string path)
        {
            if (shared is not null)
            {
                var unknown = boxes.Keys
                                  .Where(key => !mapped.ContainsKey(key) &&
                                                !(key.EndsWith("-shifted", StringComparison.Ordinal) &&
                                                  mapped.ContainsKey(key[..^"-shifted".Length])))
                                  .Order(StringComparer.Ordinal)
                                  .ToArray();
                if (unknown.Length > 0)
                    throw new InvalidDataException(
                        $"Hardware graphic {path} has callout identities absent from its shared Lua definition: {string.Join(", ", unknown)}.");
            }
        }

        /// <summary>Projects mapped controls while retaining authored labels and physical input identities.</summary>
        private static HardwareGraphicControl[] ReadGraphicControls(IReadOnlyDictionary<string, XElement> boxes,
                                                                  Dictionary<string, SharedHardwareControl> mapped, string path)
        {
            var controls = boxes
                               .Select(pair => ReadGraphicControl(pair, mapped, path))
                               .OfType<HardwareGraphicControl>()
                               .OrderBy(x => x.Control, StringComparer.Ordinal)
                               .ToArray();
            return controls;
        }

        /// <summary>Projects explicit functions, preferring pinned native physical bindings over templates.</summary>
        private static void AddExplicitCallouts(List<UiLayerCallout> callouts, JsonElement[] rows, UiLayerTemplate? template,
                                                IReadOnlyDictionary<string, XElement> boxes, PhysicalDevice device,
                                                SharedCatalogImportResult catalog, ConsumerImportResult import,
                                                DeviceProfile profile, SharedLuaImportResult? shared,
                                                HardwareGraphicControl[] controls, string path)
        {
            foreach (var row in rows)
            {
                var function = row.GetProperty("functionId").GetString()!;
                if (template is null || !template.Bindings.TryGetValue(function, out var control) ||
                    !boxes.TryGetValue(control, out var box))
                    throw new InvalidDataException(
                        $"No canonical graphic control for {device.StableKey}/{function} in {path}.");
                var command = catalog.Commands.Single(
                    x => x.Id == global::Dcs.Ipia.Domain.StableIdentity.Create("ui-function", function));
                var modifiers =
                    string.Join(" + ", row.GetProperty("modifiers").EnumerateArray().Select(x => x.GetString()));
                (control, box) = ResolveNativeControl(row, function, command, control, box, import, profile, shared, controls, boxes);
                callouts.Add(new(callouts.Count + 1, control, command.Name, modifiers,
                                 Coordinate(box, "x", path, control), Coordinate(box, "y", path, control),
                                 Size(box, "width", path, control),
                                 Size(box, "height", path, control))
                { CommandId = command.DcsCommandId });
            }
        }

        /// <summary>Adds implicit shared callouts, choosing shifted controls for modifier bindings.</summary>
        private static void AddProjectedCallouts(List<UiLayerCallout> callouts, IReadOnlyList<DcsBinding> projected,
                                                  HardwareGraphicControl[] controls)
        {
            if (projected.Count > 0)
            {
                foreach (var binding in projected)
                {
                    var candidates = controls
                                         .Where(x => x.IsAxis == (binding.Section == BindingSection.Axis) &&
                                                     DcsInputKey.SamePhysicalInput(x.InputKey, binding.InputKey))
                                         .ToArray();
                    var control =
                        (binding.Reformers.Count > 0
                             ? candidates.FirstOrDefault(x => x.Control.EndsWith("-shifted", StringComparison.Ordinal))
                             : candidates.FirstOrDefault(
                                   x => !x.Control.EndsWith("-shifted", StringComparison.Ordinal))) ??
                        candidates.FirstOrDefault();
                    if (control is null)
                        continue;
                    callouts.Add(new(callouts.Count + 1, control.Control, binding.CommandName,
                                     string.Join(" + ", binding.Reformers), control.X, control.Y, control.Width,
                                     control.Height)
                    { CommandId = binding.CommandId });
                }
            }
        }

        /// <summary>Resolves the canonical source provenance for the rendered profile.</summary>
        private static string? ReadCanonicalPath(ConsumerImportResult import, SharedLuaImportResult? shared, DeviceProfile profile)
        {
            var canonical =
                shared is null ? null : ModuleUiLayerBindingImporter.ResolveCanonicalProfile(import, shared, profile);
            var canonicalPath =
                canonical is null
                    ? null
                    : shared!.Provenance
                          .Where(x => x.Path.StartsWith("assets/shared/ui-layer/input/UiLayer/",
                                                        StringComparison.Ordinal) &&
                                      x.Path.EndsWith("/" + canonical.SourceName, StringComparison.Ordinal))
                          .Select(x => x.Path)
                          .SingleOrDefault();
            return canonicalPath;
        }

        /// <summary>Accepts only embedded PNG or JPEG image data.</summary>
        private static byte[] DecodeBackground(XElement image, string path)
        {
            var href = image.Attributes().FirstOrDefault(x => x.Name.LocalName == "href")?.Value;
            var prefix = href?.StartsWith("data:image/png;base64,", StringComparison.Ordinal) == true
                             ? "data:image/png;base64,"
                         : href?.StartsWith("data:image/jpeg;base64,", StringComparison.Ordinal) == true
                             ? "data:image/jpeg;base64,"
                             : null;
            if (prefix is null)
                throw new InvalidDataException($"Missing embedded hardware image: {path}");
            var background = Convert.FromBase64String(href![prefix.Length..]);
            return background;
        }

        /// <summary>Maps one callout, including the shared shifted-control alias convention.</summary>
        private static HardwareGraphicControl? ReadGraphicControl(KeyValuePair<string, XElement> pair,
                                                                   Dictionary<string, SharedHardwareControl> mapped, string path)
        {
            var box = pair.Value;
            if (!mapped.TryGetValue(pair.Key, out var mapping))
            {
                if (!pair.Key.EndsWith("-shifted", StringComparison.Ordinal) ||
                    !mapped.TryGetValue(pair.Key[..^"-shifted".Length], out mapping))
                    return null;
            }
            var label =
                box.ElementsAfterSelf()
                    .FirstOrDefault(x => x.Name.LocalName == "text" &&
                                         x.Attribute("id")?.Value == "lbl-" + pair.Key)
                    ?.Value.Trim() ??
                pair.Key;
            return new HardwareGraphicControl(
                pair.Key, label, mapping.InputKey,
                Coordinate(box, "x", path, pair.Key),
                Coordinate(box, "y", path, pair.Key),
                Size(box, "width", path, pair.Key),
                Size(box, "height", path, pair.Key),
                mapping.Control.Kind == global::Dcs.Ipia.Domain.ControlKind.Axis);
        }

        /// <summary>Uses an unambiguous pinned function binding before a template fallback.</summary>
        private static (string Control, XElement Box) ResolveNativeControl(JsonElement row, string function,
                DcsCommandDefinition command, string control, XElement box, ConsumerImportResult import,
                DeviceProfile profile, SharedLuaImportResult? shared, HardwareGraphicControl[] controls,
                IReadOnlyDictionary<string, XElement> boxes)
        {
            // Pinned native Lua owns physical assignments; templates only fill absent mappings.
            var canonicalProfile = shared is null ? null : ModuleUiLayerBindingImporter.ResolveCanonicalProfile(import, shared, profile);
            var chord = row.GetProperty("modifiers").EnumerateArray().Select(x => x.GetString()).Order(StringComparer.Ordinal).ToArray();
            var native = shared?.Bindings.Where(x => x.ProfileId == canonicalProfile?.Id &&
                x.Disposition != BindingDisposition.Removed && x.CommandId == command.DcsCommandId &&
                x.Reformers.Order(StringComparer.Ordinal).SequenceEqual(chord)).ToArray() ?? [];
            if (native.Length > 1)
                throw new InvalidDataException("Ambiguous pinned UI Layer function: " + function);
            if (native.Length == 1)
            {
                var candidates = controls.Where(x => x.IsAxis == (native[0].Section == BindingSection.Axis) &&
                    DcsInputKey.SamePhysicalInput(x.InputKey, native[0].InputKey)).ToArray();
                var mappedControl = candidates.FirstOrDefault(x => x.Control.EndsWith("-shifted", StringComparison.Ordinal) == (chord.Length > 0))
                    ?? candidates.FirstOrDefault() ?? throw new InvalidDataException("Pinned UI Layer function has no graphic control: " + function);
                control = mappedControl.Control;
                box = boxes[control];
            }
            return (control, box);
        }
        private static double Number(string? value) =>
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) &&
            double.IsFinite(parsed) && parsed > 0
                ? parsed
                : throw new InvalidDataException($"Invalid SVG coordinate: {value}");

        private static double Coordinate(XElement box, string attribute, string path, string control)
        {
            var value = box.Attribute(attribute)?.Value;
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) &&
                double.IsFinite(parsed) && parsed >= 0)
                return parsed;
            throw new InvalidDataException($"Hardware graphic {path} {control} has invalid {attribute}: {value}.");
        }

        private static double Size(XElement box, string attribute, string path, string control)
        {
            var value = box.Attribute(attribute)?.Value;
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) &&
                double.IsFinite(parsed) && parsed > 0)
                return parsed;
            throw new InvalidDataException($"Hardware graphic {path} {control} has invalid {attribute}: {value}.");
        }
        #endregion END private helpers
    }
}

