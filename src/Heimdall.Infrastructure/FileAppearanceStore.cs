using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Heimdall.Application;
using Heimdall.Application.Interfaces;

namespace Heimdall.Infrastructure
{
    /// <summary>Keeps appearance JSON and content-addressed PNGs inside the full-backup profile tree.</summary>
    public sealed class FileAppearanceStore : IAppearanceStore
    {
        private readonly string _directory;
        private readonly string _path;
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
        /// <summary>Loads appearance overrides from the selected profile without changing unrelated settings.</summary>
        public FileAppearanceStore(string profileDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(profileDirectory);
            _directory = Path.Combine(Path.GetFullPath(profileDirectory), "appearance");
            _path = Path.Combine(_directory, "settings.json");
            try
            {
                if (File.Exists(_path)) Current = AppearanceValidation.ParseSettings(ReadJson(_path));
            }
            catch (Exception ex) when (IsRecoverable(ex)) { LastError = "Appearance settings could not be loaded; using the built-in skin. " + ex.Message; }
        }
        #region BEGIN Public
        /// <summary>Maximum normalized image payload accepted into a profile.</summary>
        public const int MaximumImageBytes = 20 * 1024 * 1024;
        /// <inheritdoc/>
        public AppearanceSettings Current { get; private set; } = new();
        /// <inheritdoc/>
        public string? LastError { get; private set; }
        /// <inheritdoc/>
        public byte[]? ReadImage(string? name = null)
        {
            name ??= Current.Image;
            if (name is null) return null;
            try
            {
                AppearanceValidation.Validate(new AppearanceSettings(Image: name));
                var path = ImagePath(name);
                if (new FileInfo(path).Length > MaximumImageBytes) throw new ArgumentException("Background image exceeds 20 MiB.");
                var bytes = File.ReadAllBytes(path);
                CheckImage(bytes);
                if (Convert.ToHexStringLower(SHA256.HashData(bytes)) + ".png" != name)
                    throw new ArgumentException("Background image checksum differs from its saved reference.");
                return bytes;
            }
            catch (Exception ex) when (IsRecoverable(ex)) { LastError = "Background image is unavailable; choose it again in Appearance. " + ex.Message; return null; }
        }
        /// <inheritdoc/>
        public bool TrySave(AppearanceSettings settings, byte[]? image = null, IReadOnlyDictionary<string, byte[]>? images = null)
        {
            var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var newImages = new List<string>();
            try
            {
                AppearanceValidation.Validate(settings);
                if (image is not null)
                {
                    CheckImage(image);
                    settings = settings with { Image = Convert.ToHexStringLower(SHA256.HashData(image)) + ".png" };
                }
                Directory.CreateDirectory(_directory);
                var pending = images is null ? new Dictionary<string, byte[]>(StringComparer.Ordinal) : new Dictionary<string, byte[]>(images, StringComparer.Ordinal);
                if (image is not null) pending[settings.Image!] = image;
                foreach (var name in AppearanceTargets.Images(settings))
                {
                    AppearanceValidation.Validate(new AppearanceSettings(Image: name));
                    var path = ImagePath(name);
                    if (pending.TryGetValue(name, out var bytes))
                    {
                        CheckImage(bytes);
                        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) + ".png" != name) throw new ArgumentException("Background image checksum differs from its reference.");
                        if (!File.Exists(path)) { File.WriteAllBytes(path, bytes); newImages.Add(path); }
                        else if (!File.ReadAllBytes(path).SequenceEqual(bytes)) throw new ArgumentException("Existing managed image is corrupt; choose another image or restore your backup.");
                    }
                    if (ReadImage(name) is null) throw new ArgumentException("The selected managed image is missing or corrupt; choose it again.");
                }
                var json = JsonSerializer.Serialize(settings, JsonOptions);
                _ = AppearanceValidation.ParseSettings(json);
                File.WriteAllText(temporary, json);
                File.Move(temporary, _path, true);
                Current = AppearanceValidation.ParseSettings(json);
                LastError = null;
                newImages.Clear();
                RemoveUnusedImages();
                return true;
            }
            catch (Exception ex) when (IsRecoverable(ex)) { LastError = "Appearance was not saved; the previous settings are intact. " + ex.Message; return false; }
            finally { DeleteTemporary(temporary); foreach (var path in newImages) DeleteTemporary(path); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private string ImagePath(string name) => Path.Combine(_directory, name);
        private static string ReadJson(string path)
        {
            if (new FileInfo(path).Length > AppearanceValidation.MaximumJsonBytes) throw new ArgumentException("Appearance settings exceed 64 KiB.");
            return File.ReadAllText(path);
        }
        private void RemoveUnusedImages()
        {
            try
            {
                foreach (var path in Directory.EnumerateFiles(_directory, "*.png"))
                {
                    var name = Path.GetFileName(path);
                    if (!AppearanceTargets.Images(Current).Contains(name, StringComparer.Ordinal) && name.Length == 68 && name.AsSpan(0, 64).ToString().All(Uri.IsHexDigit)) DeleteTemporary(path);
                }
            }
            catch (IOException) { /* A later successful save can remove unused content. */ }
            catch (UnauthorizedAccessException) { /* The accepted JSON and image remain usable. */ }
        }
        private static void CheckImage(byte[] bytes)
        {
            if (bytes.Length > MaximumImageBytes || bytes.Length < 33 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
                throw new ArgumentException("Choose a valid normalized PNG no larger than 20 MiB.");
            var width = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4));
            var height = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4));
            if (width == 0 || height == 0 || width > 8192 || height > 8192 || (long)width * height > 24000000)
                throw new ArgumentException("Background dimensions exceed the supported image budget.");
        }
        private static bool IsRecoverable(Exception ex) => ex is IOException or UnauthorizedAccessException or ArgumentException or JsonException;
        private static void DeleteTemporary(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { /* A temporary file is never consumed as a saved appearance. */ }
            catch (UnauthorizedAccessException) { /* Retain the original save error. */ }
        }
        #endregion END private helpers
    }
}
