using System.Text.Json;
using System.Xml.Linq;
using Dcs.Ipia.Domain.MultiSim;

namespace Dcs.Ipia.EliteDangerous
{
    /// <summary>Projects native slots while retaining device tokens and cross-device modifier identity.</summary>
    internal static class EliteProfileProjection
    {
        #region BEGIN Public
        /// <summary>Creates an independent portable projection from native evidence.</summary>
        public static ProfileDocument Create(string context, EliteNativeDocument native)
        {
            var profile = new ProfileDocument
            {
                BackendId = "elite-dangerous", ContextId = context, ProfileId = native.Path,
                Name = EliteNativeDocument.Attribute(native.Root, "PresetName")
            };
            foreach (var command in native.Commands)
            {
                var id = CommandId(context, command.Name.LocalName);
                if (command.Element("ToggleOn") is { } toggle)
                {
                    profile.Metadata.Add("ed.toggle:" + id, EliteNativeDocument.Flag(toggle) ? "1" : "0");
                }
                foreach (var slot in command.Elements().Where(x => EliteNativeDocument.IsSlot(x.Name.LocalName)))
                {
                    AddSlot(profile, command, slot, id);
                }
            }
            return profile;
        }

        /// <summary>Gets the scoped catalog identity without guessing human-readable labels.</summary>
        public static string CommandId(string context, string command) =>
            $"elite-dangerous:{Uri.EscapeDataString(context)}:{Uri.EscapeDataString(command)}";

        /// <summary>Gets the exact native device reference, including an optional index.</summary>
        public static PhysicalDeviceIdentity Identity(XElement node)
        {
            var token = EliteNativeDocument.Attribute(node, "Device");
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException("An Elite device token cannot be empty.");
            }
            var identity = new PhysicalDeviceIdentity();
            identity.SimSpecificIds.Add("ed.device", token);
            if (node.Attribute("DeviceIndex") is { } index)
            {
                if (!uint.TryParse(index.Value, System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out _))
                {
                    throw new InvalidOperationException("Elite DeviceIndex must be a nonnegative integer.");
                }
                identity.SimSpecificIds.Add("ed.deviceIndex", index.Value);
            }
            return identity;
        }

        /// <summary>Gets a logical reference; this deliberately does not assert a Windows instance.</summary>
        public static string DeviceId(PhysicalDeviceIdentity identity) => "ed:" +
            Uri.EscapeDataString(identity.SimSpecificIds["ed.device"]) + ":" +
            (identity.SimSpecificIds.TryGetValue("ed.deviceIndex", out var index) ? index : "~");

        /// <summary>Compares JSON-safe portable values, including deserialized extension values.</summary>
        public static bool Equal<T>(T first, T second) => JsonSerializer.Serialize(first) == JsonSerializer.Serialize(second);
        #endregion END Public

        #region BEGIN private helpers
        private static void AddSlot(ProfileDocument profile, XElement command, XElement slot, string id)
        {
            var identity = Identity(slot);
            var key = EliteNativeDocument.Attribute(slot, "Key");
            // Validate unbound axes too; otherwise bad filter values could survive a later assignment.
            if (slot.Name == "Binding")
            {
                _ = EliteNativeDocument.Flag(Required(command, "Inverted"));
                _ = EliteNativeDocument.Deadzone(Required(command, "Deadzone"));
            }
            if (identity.SimSpecificIds["ed.device"] == "{NoDevice}")
            {
                if (key.Length != 0 || slot.HasElements)
                {
                    throw new InvalidOperationException("Unbound Elite slots must have an empty key and no modifiers.");
                }
                return;
            }
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException("Bound Elite slots require an input key.");
            }
            var device = GetDevice(profile, identity);
            var modifiers = ReadModifiers(profile, slot);
            var extensions = new Dictionary<string, object> { ["ed.slot"] = slot.Name.LocalName };
            if (slot.Name == "Binding")
            {
                device.Axes.Add(new AxisBinding
                {
                    SourceAxis = key, CommandId = id, Modifiers = modifiers, SimSpecific = extensions,
                    Invert = EliteNativeDocument.Flag(Required(command, "Inverted")),
                    Deadzone = (float)(EliteNativeDocument.Deadzone(Required(command, "Deadzone")) * 100)
                });
            }
            else
            {
                device.Buttons.Add(new ButtonBinding
                {
                    SourceButton = key, CommandId = id, Modifiers = modifiers, SimSpecific = extensions,
                    Mode = slot.Element("Hold") is { } hold && EliteNativeDocument.Flag(hold) ? BindingMode.Hold : BindingMode.Press
                });
            }
        }

        private static List<Modifier> ReadModifiers(ProfileDocument profile, XElement slot)
        {
            var result = new List<Modifier>();
            foreach (var modifier in slot.Elements("Modifier"))
            {
                var identity = Identity(modifier);
                var key = EliteNativeDocument.Attribute(modifier, "Key");
                if (identity.SimSpecificIds["ed.device"] == "{NoDevice}" || string.IsNullOrWhiteSpace(key))
                {
                    throw new InvalidOperationException("Elite modifiers require a device and key.");
                }
                result.Add(new Modifier { Source = GetDevice(profile, identity).LogicalId, Value = key });
            }
            return result;
        }

        private static LogicalDevice GetDevice(ProfileDocument profile, PhysicalDeviceIdentity identity)
        {
            var id = DeviceId(identity);
            var existing = profile.Devices.Find(x => x.LogicalId == id);
            if (existing is not null)
            {
                return existing;
            }
            var device = new LogicalDevice { LogicalId = id, ContextId = profile.ContextId, Identity = identity };
            profile.Devices.Add(device);
            return device;
        }

        private static XElement Required(XElement node, string name) => node.Element(name)
            ?? throw new InvalidOperationException($"Missing Elite {name} setting.");
        #endregion END private helpers
    }
}
