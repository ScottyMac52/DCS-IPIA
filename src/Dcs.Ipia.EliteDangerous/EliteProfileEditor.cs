using System.Globalization;
using System.Xml.Linq;
using Dcs.Ipia.Domain.MultiSim;

namespace Dcs.Ipia.EliteDangerous
{
    /// <summary>Edits only selected native slots and flags, retaining all unrelated XML and selector text.</summary>
    internal static class EliteProfileEditor
    {
        #region BEGIN Public
        /// <summary>Validates all changes before returning the complete in-memory output.</summary>
        public static Dictionary<string, string> Export(ProfileDocument profile, ProfileDocument baseline, EliteNativeDocument native)
        {
            EliteProfileValidation.Validate(profile, baseline);
            var before = Assignments(baseline);
            var after = Assignments(profile);
            var known = new HashSet<string>(StringComparer.Ordinal);
            var changed = false;
            foreach (var command in native.Commands)
            {
                var id = EliteProfileProjection.CommandId(profile.ContextId, command.Name.LocalName);
                foreach (var slot in command.Elements().Where(x => EliteNativeDocument.IsSlot(x.Name.LocalName)).ToArray())
                {
                    var address = id + "/" + slot.Name.LocalName;
                    known.Add(address);
                    before.TryGetValue(address, out var oldBinding);
                    after.TryGetValue(address, out var newBinding);
                    if (!EliteProfileProjection.Equal(oldBinding, newBinding))
                    {
                        WriteSlot(slot, command, newBinding, oldBinding, profile);
                        changed = true;
                    }
                }
                changed |= WriteToggle(command, id, profile, baseline);
            }
            if (after.Keys.Any(x => !known.Contains(x)))
            {
                throw new InvalidOperationException("Assignment targets an unknown command or native slot.");
            }
            var files = new Dictionary<string, string>(native.Sources, StringComparer.Ordinal);
            if (changed)
            {
                var declaration = native.Xml.Declaration;
                files[native.Path] = (declaration is null ? "" : declaration + "\n") + native.Xml.ToString(SaveOptions.DisableFormatting);
            }
            return files;
        }
        #endregion END Public

        #region BEGIN private helpers
        private static Dictionary<string, EliteAssignment> Assignments(ProfileDocument profile)
        {
            var result = new Dictionary<string, EliteAssignment>(StringComparer.Ordinal);
            foreach (var device in profile.Devices)
            {
                foreach (var button in device.Buttons)
                {
                    EliteProfileValidation.Button(button);
                    Add(result, new(device.LogicalId, button.SourceButton, button.CommandId,
                        EliteProfileValidation.Slot(button.SimSpecific), button.Modifiers, button.Mode, null));
                }
                foreach (var axis in device.Axes)
                {
                    EliteProfileValidation.Axis(axis);
                    Add(result, new(device.LogicalId, axis.SourceAxis, axis.CommandId,
                        EliteProfileValidation.Slot(axis.SimSpecific), axis.Modifiers, BindingMode.Press, axis));
                }
            }
            foreach (var binding in result.Values)
            {
                ValidateModifiers(binding, profile);
            }
            return result;
        }

        private static void Add(Dictionary<string, EliteAssignment> result, EliteAssignment binding)
        {
            if (string.IsNullOrWhiteSpace(binding.Key) || !result.TryAdd(binding.Id, binding))
            {
                throw new InvalidOperationException("Elite assignment keys must be nonempty and native slots must be unique.");
            }
        }

        private static void ValidateModifiers(EliteAssignment binding, ProfileDocument profile)
        {
            if (binding.Modifiers is null)
            {
                throw new InvalidOperationException("Elite modifier collections cannot be null.");
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var modifier in binding.Modifiers)
            {
                if (modifier is null || !profile.Devices.Any(x => x.LogicalId == modifier.Source) || string.IsNullOrWhiteSpace(modifier.Value)
                    || !seen.Add(modifier.Source + "/" + modifier.Value))
                {
                    throw new InvalidOperationException("Modifiers need a captured device and a unique nonempty input key.");
                }
            }
        }

        private static void WriteSlot(XElement slot, XElement command, EliteAssignment? binding,
            EliteAssignment? original, ProfileDocument profile)
        {
            ValidateEditableSlot(slot);
            if (binding?.Axis is not null && binding.Modifiers.Count != 0)
            {
                throw new InvalidOperationException("Elite axis modifier chording has not been verified; this edit is unsupported.");
            }
            slot.Elements().Remove();
            slot.Attributes().Remove();
            if (binding is null)
            {
                slot.SetAttributeValue("Device", "{NoDevice}");
                slot.SetAttributeValue("Key", "");
                return;
            }
            SetReference(slot, binding.DeviceId, binding.Key, profile);
            foreach (var modifier in binding.Modifiers)
            {
                var element = new XElement("Modifier");
                SetReference(element, modifier.Source, modifier.Value!, profile);
                slot.Add(element);
            }
            if (binding.Mode == BindingMode.Hold)
            {
                slot.Add(new XElement("Hold", new XAttribute("Value", "1")));
            }
            if (binding.Axis is { } axis)
            {
                WriteAxis(command, axis, original?.Axis);
            }
        }

        private static void SetReference(XElement node, string deviceId, string key, ProfileDocument profile)
        {
            var identity = profile.Devices.Single(x => x.LogicalId == deviceId).Identity!;
            node.SetAttributeValue("Device", identity.SimSpecificIds["ed.device"]);
            if (identity.SimSpecificIds.TryGetValue("ed.deviceIndex", out var index))
            {
                node.SetAttributeValue("DeviceIndex", index);
            }
            node.SetAttributeValue("Key", key);
        }

        private static void WriteAxis(XElement command, AxisBinding axis, AxisBinding? original)
        {
            command.Element("Inverted")!.SetAttributeValue("Value", axis.Invert ? "1" : "0");
            // Leave the original native double untouched when the portable float has not changed.
            if (original is null || axis.Deadzone != original.Deadzone)
            {
                command.Element("Deadzone")!.SetAttributeValue("Value", ((double)axis.Deadzone / 100).ToString("R", CultureInfo.InvariantCulture));
            }
        }

        private static bool WriteToggle(XElement command, string id, ProfileDocument profile, ProfileDocument baseline)
        {
            var key = "ed.toggle:" + id;
            if (!profile.Metadata.TryGetValue(key, out var value) || value == baseline.Metadata[key])
            {
                return false;
            }
            command.Element("ToggleOn")!.SetAttributeValue("Value", value);
            return true;
        }

        private static void ValidateEditableSlot(XElement slot)
        {
            if (slot.Attributes().Any(x => x.Name.Namespace != XNamespace.None || x.Name.LocalName is not "Device" and not "DeviceIndex" and not "Key")
                || slot.Nodes().Any(x => x is not XElement && (x is not XText text || !string.IsNullOrWhiteSpace(text.Value)))
                || slot.Elements("Hold").Count() > 1)
            {
                throw new InvalidOperationException("Unknown native slot attributes or comments prevent rewriting this assignment.");
            }
            foreach (var child in slot.Elements())
            {
                var allowed = child.Name == "Modifier" ? new[] { "Device", "DeviceIndex", "Key" } : new[] { "Value" };
                if (child.Name != "Modifier" && child.Name != "Hold" || child.Nodes().Any(x => x is not XText text || !string.IsNullOrWhiteSpace(text.Value))
                    || slot.Name == "Binding"
                    || child.Attributes().Any(x => x.Name.Namespace != XNamespace.None || !allowed.Contains(x.Name.LocalName, StringComparer.Ordinal)))
                {
                    throw new InvalidOperationException("Unknown native binding extensions prevent rewriting this assignment.");
                }
            }
        }
        #endregion END private helpers
    }
}
