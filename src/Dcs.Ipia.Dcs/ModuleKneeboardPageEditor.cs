using System.Text.Json;
using System.Text.Json.Nodes;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Dcs
{
    /// <summary>Stages supported page layouts without losing authored manifest metadata, labels or bindings.</summary>
    public static class ModuleKneeboardPageEditor
    {
        private const string Manifest = "config/kneeboard.json";
        #region BEGIN Public
        /// <summary>Reads editable metadata from an existing authored page.</summary>
        /// <param name="workspace">Current module draft.</param>
        /// <param name="page">Selected authored page.</param>
        /// <returns>Page metadata and ordered summary cards.</returns>
        public static ModuleKneeboardPageSettings Describe(EditorWorkspaceSnapshot workspace, KneeboardPage page)
        {
            Current(workspace, page);
            var root = Document(workspace);
            var row = Rows(root, page.Kind).Single(x => x?["file"]?.GetValue<string>() == page.StableKey)!;
            var profile = page.Kind == KneeboardPageKind.Device
                ? workspace.Configuration.Profiles.Single(x => Path.GetFileName(root["profiles"]![row["profile"]!.GetValue<string>()]!.GetValue<string>()) == x.SourceName).Id
                : (Guid?)null;
            var cards = row["items"] is JsonArray items ? items.Select(x => new ModuleSummaryCard(
                x!["key"]!.GetValue<string>(), x["text"]!.GetValue<string>(), x["accent"]?.GetValue<string>() ?? "cyan")).ToArray() : [];
            return new(page.Kind, profile, page.StableKey, row["title"]?.GetValue<string>() ?? page.Title,
                row["kicker"]?.GetValue<string>() ?? "", page.Order, cards);
        }

        /// <summary>Adds or updates a page while preserving its stable identity and unedited manifest fields.</summary>
        /// <param name="workspace">Current module draft.</param>
        /// <param name="current">Selected original page, or null to add.</param>
        /// <param name="settings">Supported page metadata and cards.</param>
        /// <returns>Updated module draft with synchronized page and manifest state.</returns>
        public static EditorWorkspaceSnapshot Set(EditorWorkspaceSnapshot workspace, KneeboardPage? current, ModuleKneeboardPageSettings settings)
        {
            Validate(settings);
            if (current is not null)
            {
                Current(workspace, current);
                if (current.Kind != settings.Kind || Describe(workspace, current).ProfileId != settings.ProfileId)
                    throw new InvalidOperationException("Keep the page layout and device identity. Add a separate summary or edit the device instead.");
            }
            if (workspace.Pages.Any(x => x.Id != current?.Id && x.StableKey.Equals(settings.File, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("That output page name already exists.");
            var root = Document(workspace);
            var rows = Rows(root, settings.Kind);
            JsonObject row;
            if (current is null)
            {
                row = new JsonObject();
                rows.Add(row);
            }
            else
                row = rows.Single(x => x?["file"]?.GetValue<string>() == current.StableKey)!.AsObject();
            if (settings.Kind == KneeboardPageKind.Device)
            {
                var profile = workspace.Configuration.Profiles.SingleOrDefault(x => x.Id == settings.ProfileId)
                    ?? throw new InvalidOperationException("Select a device from this module.");
                if (!workspace.Diagrams.Any(x => x.ProfileId == profile.Id && x.Controls.Count > 0 && x.Svg.Length > 0))
                    throw new InvalidOperationException("This device needs a pinned graphic before authoring its page.");
                var profiles = root["profiles"]!.AsObject();
                var matches = profiles.Where(x => Path.GetFileName(x.Value!.GetValue<string>()) == profile.SourceName).ToArray();
                if (matches.Length > 1)
                    throw new InvalidOperationException("The device has ambiguous page mappings.");
                var profileKey = matches.Length == 1 ? matches[0].Key : "device-" + profile.Id.ToString("N");
                if (rows.Any(x => !ReferenceEquals(x, row) && x?["profile"]?.GetValue<string>() == profileKey))
                    throw new InvalidOperationException("Each device has one combined page. Edit its existing page.");
                if (matches.Length == 0)
                    profiles[profileKey] = $"src/Config/Input/{workspace.Configuration.Module.DcsId}/joystick/{profile.SourceName}";
                row["profile"] = profileKey;
                row["deviceId"] = profile.DeviceId;
            }
            else
            {
                row["type"] = "summary";
                var originalItems = row["items"] as JsonArray;
                var items = new JsonArray();
                for (var i = 0; i < settings.Cards.Count; i++)
                {
                    var matching = originalItems?.Where(x => x?["key"]?.GetValue<string>() == settings.Cards[i].Key).ToArray() ?? [];
                    var original = matching.Length == 1 ? matching[0] : originalItems is not null && i < originalItems.Count ? originalItems[i] : null;
                    var item = original?.DeepClone().AsObject() ?? new JsonObject();
                    item["key"] = settings.Cards[i].Key;
                    item["text"] = settings.Cards[i].Text;
                    item["accent"] = settings.Cards[i].Accent;
                    items.Add(item);
                }
                row["items"] = items;
            }
            row["file"] = settings.File;
            row["title"] = settings.Title;
            row["kicker"] = settings.Subtitle;
            var page = new KneeboardPage(current?.Id ?? Guid.NewGuid(), workspace.Configuration.Module.Id, current?.DeviceInstanceId,
                settings.Kind, settings.File, settings.Title, current?.CategoryIds ?? [], settings.Order);
            return Store(workspace with
            {
                Pages = workspace.Pages.Where(x => x.Id != current?.Id).Append(page).OrderBy(x => x.Order).ThenBy(x => x.StableKey, StringComparer.Ordinal).ToArray(),
                Images = current is null ? workspace.Images : workspace.Images.Where(x => x.PageKey != current.StableKey).ToArray()
            }, root);
        }

        /// <summary>Removes an overview page without affecting devices or assignments.</summary>
        /// <param name="workspace">Current module draft.</param>
        /// <param name="current">Selected overview page.</param>
        /// <returns>Updated module draft.</returns>
        public static EditorWorkspaceSnapshot Remove(EditorWorkspaceSnapshot workspace, KneeboardPage current)
        {
            Current(workspace, current);
            if (current.Kind == KneeboardPageKind.Device)
                throw new InvalidOperationException("Each device requires one combined page. Edit its page or remove the device.");
            var root = Document(workspace);
            var rows = Rows(root, current.Kind);
            rows.Remove(rows.Single(x => x?["file"]?.GetValue<string>() == current.StableKey));
            return Store(workspace with { Pages = workspace.Pages.Where(x => x.Id != current.Id).ToArray(),
                Images = workspace.Images.Where(x => x.PageKey != current.StableKey).ToArray() }, root);
        }

        /// <summary>Creates missing combined pages for every pinned device, including inherited UI Layer devices.</summary>
        /// <param name="workspace">Current module draft.</param>
        /// <returns>Draft with existing pages preserved and missing device pages appended.</returns>
        public static EditorWorkspaceSnapshot AddMissingDevicePages(EditorWorkspaceSnapshot workspace)
        {
            foreach (var profile in workspace.Configuration.Profiles)
                if (KneeboardPageLocator.PageKeyForProfile(workspace, profile.Id) is null)
                    workspace = Set(workspace, null, new(KneeboardPageKind.Device, profile.Id, "device-" + profile.Id.ToString("N"),
                        workspace.Diagrams.Single(x => x.ProfileId == profile.Id).Title, "DEVICE CONTROLS",
                        workspace.Pages.Count == 0 ? 0 : workspace.Pages.Max(x => x.Order) + 1, []));
            return Store(workspace, Document(workspace));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void Validate(ModuleKneeboardPageSettings settings)
        {
            if (!Enum.IsDefined(settings.Kind) || settings.Order < 0 ||
                string.IsNullOrWhiteSpace(settings.File) || !settings.File.All(x => char.IsAsciiLetterOrDigit(x) || x is '-' or '_') ||
                string.IsNullOrWhiteSpace(settings.Title) || settings.Title.Any(char.IsControl) || settings.Subtitle.Any(char.IsControl))
                throw new InvalidOperationException("Enter a safe page name (letters, numbers, hyphens or underscores), heading and non-negative order.");
            if (settings.Kind == KneeboardPageKind.Summary && (settings.ProfileId is not null || string.IsNullOrWhiteSpace(settings.Subtitle) ||
                settings.Cards.Count is < 1 or > 14 || settings.Cards.Any(x => string.IsNullOrWhiteSpace(x.Key) || string.IsNullOrWhiteSpace(x.Text) ||
                    x.Key.Any(char.IsControl) || x.Text.Any(char.IsControl) || x.Accent is not ("cyan" or "gold" or "red" or "blue" or "orange" or "purple" or "green"))))
                throw new InvalidOperationException("Summary pages need a subtitle and one to fourteen cards with a heading, description and supported accent.");
            if (settings.Kind == KneeboardPageKind.Device && settings.Cards.Count != 0)
                throw new InvalidOperationException("Device pages use the pinned hardware layout; cards belong to summary pages.");
        }
        private static void Current(EditorWorkspaceSnapshot workspace, KneeboardPage page)
        {
            if (!workspace.Pages.Any(x => x == page) || page.ModuleId != workspace.Configuration.Module.Id)
                throw new InvalidOperationException("The selected page changed. Select it again.");
        }
        private static JsonObject Document(EditorWorkspaceSnapshot workspace)
        {
            var root = JsonNode.Parse(workspace.Sources?.GetValueOrDefault(Manifest) ?? "{}")!.AsObject();
            root["profiles"] ??= new JsonObject();
            root["pages"] ??= new JsonArray();
            root["summaryPages"] ??= new JsonArray();
            return root;
        }
        private static JsonArray Rows(JsonObject root, KneeboardPageKind kind) =>
            root[kind == KneeboardPageKind.Device ? "pages" : "summaryPages"]!.AsArray();
        private static EditorWorkspaceSnapshot Store(EditorWorkspaceSnapshot workspace, JsonObject root)
        {
            var sources = new Dictionary<string, string>(workspace.Sources ?? new Dictionary<string, string>(), StringComparer.Ordinal)
            {
                [Manifest] = root.ToJsonString()
            };
            return workspace with { Sources = sources };
        }
        #endregion END private helpers
    }
}
