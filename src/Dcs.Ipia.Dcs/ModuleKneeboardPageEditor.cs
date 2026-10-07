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
            var cards = row["items"] is JsonArray items ? items.Select((x, index) => new ModuleSummaryCard(
                x!["key"]!.GetValue<string>(), x["text"]!.GetValue<string>(), x["accent"]?.GetValue<string>() ?? "cyan")
                { SourceIndex = index }).ToArray() : [];
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
                    if (originalItems is not null && settings.Cards[i].SourceIndex is int sourceIndex &&
                        sourceIndex >= 0 && sourceIndex < originalItems.Count)
                        original = originalItems[sourceIndex];
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

        /// <summary>Reports whether the selected page belongs to the current module draft and can be removed.</summary>
        /// <param name="workspace">Current module draft.</param>
        /// <param name="page">Selected page.</param>
        /// <returns>True for any current page, including device and orphan pages.</returns>
        public static bool CanRemove(EditorWorkspaceSnapshot workspace, KneeboardPage page)
        {
            Current(workspace, page);
            return true;
        }

        /// <summary>Removes any page and compacts page order without affecting devices or assignments.</summary>
        /// <param name="workspace">Current module draft.</param>
        /// <param name="current">Selected page.</param>
        /// <returns>Updated module draft.</returns>
        public static EditorWorkspaceSnapshot Remove(EditorWorkspaceSnapshot workspace, KneeboardPage current)
        {
            Current(workspace, current);
            var root = Document(workspace);
            return WithoutPages(workspace, root, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { current.StableKey });
        }

        /// <summary>Removes every manifest alias and page owned by a deleted profile, preserving other physical units.</summary>
        /// <param name="workspace">Current module draft.</param>
        /// <param name="profile">Profile whose final device membership is being removed.</param>
        /// <returns>Draft with associated pages, cached images and mappings removed and remaining orders compacted.</returns>
        public static EditorWorkspaceSnapshot RemoveDevicePages(EditorWorkspaceSnapshot workspace, DeviceProfile profile)
        {
            if (!workspace.Configuration.Profiles.Contains(profile))
                throw new InvalidOperationException("Select a current device from this module.");
            var root = Document(workspace);
            var profiles = root["profiles"]!.AsObject();
            var keys = profiles.Where(x => x.Key == DeviceKey(profile) ||
                FileName(x.Value?.GetValue<string>()).Equals(profile.SourceName, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
            keys.Add(DeviceKey(profile));
            var pages = Rows(root, KneeboardPageKind.Device).Where(x => keys.Contains(x?["profile"]?.GetValue<string>() ?? ""))
                .Select(x => x!["file"]!.GetValue<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            pages.Add(DeviceKey(profile));
            foreach (var key in keys) profiles.Remove(key);
            return WithoutPages(workspace, root, pages);
        }

        /// <summary>Appends summaries listing all current device pages in display order, with up to fourteen cards per summary.</summary>
        /// <param name="workspace">Current module draft.</param>
        /// <returns>Draft with new summary pages and all existing pages preserved.</returns>
        public static EditorWorkspaceSnapshot GenerateSummary(EditorWorkspaceSnapshot workspace)
        {
            var cards = workspace.Pages.OrderBy(x => x.Order).ThenBy(x => x.StableKey, StringComparer.Ordinal)
                .Select((page, index) => (Page: page, Number: index + 1))
                .Where(x => x.Page.Kind == KneeboardPageKind.Device)
                .Select(x => new ModuleSummaryCard(x.Number.ToString("D2", System.Globalization.CultureInfo.InvariantCulture), x.Page.Title, "cyan")).ToArray();
            if (cards.Length == 0)
                throw new InvalidOperationException("Add device pages before generating a summary.");
            foreach (var batch in cards.Chunk(14))
            {
                var file = "summary";
                for (var suffix = 2; workspace.Pages.Any(x => x.StableKey.Equals(file, StringComparison.OrdinalIgnoreCase)); suffix++)
                    file = "summary-" + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
                workspace = Set(workspace, null, new(KneeboardPageKind.Summary, null, file,
                    workspace.Configuration.Module.DisplayName + " — Summary", "DEVICE PAGES", workspace.Pages.Max(x => x.Order) + 1, batch));
            }
            return workspace;
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
        private static string DeviceKey(DeviceProfile profile) => "device-" + profile.Id.ToString("N");
        private static string FileName(string? path) => (path ?? "").Replace('\\', '/').Split('/')[^1];
        private static EditorWorkspaceSnapshot WithoutPages(EditorWorkspaceSnapshot workspace, JsonObject root, HashSet<string> keys)
        {
            var removedProfiles = Rows(root, KneeboardPageKind.Device).Where(x => keys.Contains(x?["file"]?.GetValue<string>() ?? ""))
                .Select(x => x?["profile"]?.GetValue<string>()).Where(x => x is not null).ToArray();
            foreach (var kind in new[] { KneeboardPageKind.Device, KneeboardPageKind.Summary })
            {
                var rows = Rows(root, kind);
                foreach (var row in rows.Where(x => keys.Contains(x?["file"]?.GetValue<string>() ?? "")).ToArray())
                    rows.Remove(row);
            }
            foreach (var profile in removedProfiles)
                if (!Rows(root, KneeboardPageKind.Device).Any(x => x?["profile"]?.GetValue<string>() == profile))
                    root["profiles"]!.AsObject().Remove(profile!);
            var pages = workspace.Pages.Where(x => !keys.Contains(x.StableKey))
                .OrderBy(x => x.Order).ThenBy(x => x.StableKey, StringComparer.Ordinal)
                .Select((page, order) => page with { Order = order }).ToArray();
            return Store(workspace with { Pages = pages, Images = workspace.Images.Where(x => !keys.Contains(x.PageKey)).ToArray() }, root);
        }
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
