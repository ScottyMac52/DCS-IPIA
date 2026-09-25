using DcsConsumerScaffold.Models;

namespace DcsConsumerScaffold.Services;

public static class UiLayerEditorSelection
{
    public static IReadOnlyList<UiLayerCatalogProfile> ProfilesForDevice(
        UiLayerCatalogDocument document, UiLayerDevicePossibility device)
    {
        var deviceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { device.DeviceId, device.CanonicalDeviceId };
        var profileNames = document.Bindings
            .Where(binding => binding.DeviceId is not null && deviceIds.Contains(binding.DeviceId))
            .Select(binding => binding.Profile)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return document.Profiles
            .Where(profile => profileNames.Contains(profile.Name))
            .OrderBy(profile => profile.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
    public static UiLayerBindingEdit DeleteBinding(UiLayerCatalogBinding binding) => new()
    {
        Action = "clear",
        Command = binding.Command,
        Name = binding.Name,
        Profile = new UiLayerProfileTarget
        {
            Category = binding.Category,
            Filename = binding.Profile,
            DeviceId = binding.DeviceId ?? string.Empty,
        },
        Section = binding.Section,
        Key = binding.Key,
        Reformers = [.. binding.Modifiers],
    };

    public static UiLayerBindingEdit DeleteProfile(UiLayerCatalogProfile profile) => new()
    {
        Action = "delete-profile",
        Profile = new UiLayerProfileTarget { Category = profile.Category, Filename = profile.Name },
    };

    public static IReadOnlyList<UiLayerCatalogProfile> ProjectProfiles(
        UiLayerCatalogDocument document, IEnumerable<UiLayerBindingEdit> edits)
    {
        var deleted = edits.Where(edit => edit.Action == "delete-profile")
            .Select(edit => $"{edit.Profile?.Category}\0{edit.Profile?.Filename}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return document.Profiles.Where(profile => !deleted.Contains($"{profile.Category}\0{profile.Name}")).ToList();
    }

    public static IReadOnlyList<UiLayerCatalogBinding> ProjectBindings(
        UiLayerCatalogDocument document, IEnumerable<UiLayerBindingEdit> edits)
    {
        var staged = edits.ToList();
        return document.Bindings.Where(binding =>
            !staged.Any(edit => edit.Action == "delete-profile"
                && edit.Profile?.Category.Equals(binding.Category, StringComparison.OrdinalIgnoreCase) == true
                && edit.Profile.Filename.Equals(binding.Profile, StringComparison.OrdinalIgnoreCase))
            && !staged.Any(edit => edit.Action == "clear"
                && edit.Profile?.Category.Equals(binding.Category, StringComparison.OrdinalIgnoreCase) == true
                && edit.Profile.Filename.Equals(binding.Profile, StringComparison.OrdinalIgnoreCase)
                && edit.Command == binding.Command && edit.Key == binding.Key
                && edit.Reformers.SequenceEqual(binding.Modifiers, StringComparer.Ordinal))).ToList();
    }
}
