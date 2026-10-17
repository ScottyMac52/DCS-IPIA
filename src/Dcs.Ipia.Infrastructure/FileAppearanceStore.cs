using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;

namespace Dcs.Ipia.Infrastructure
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
        public byte[]? ReadImage()
        {
            if (Current.Image is null) return null;
            try
            {
                var path = ImagePath(Current.Image);
                if (new FileInfo(path).Length > MaximumImageBytes) throw new ArgumentException("Background image exceeds 20 MiB.");
                var bytes = File.ReadAllBytes(path);
                CheckImage(bytes);
                if (Convert.ToHexStringLower(SHA256.HashData(bytes)) + ".png" != Current.Image)
                    throw new ArgumentException("Background image checksum differs from its saved reference.");
                return bytes;
            }
            catch (Exception ex) when (IsRecoverable(ex)) { LastError = "Background image is unavailable; choose it again in Appearance. " + ex.Message; return null; }
        }
        /// <inheritdoc/>
        public bool TrySave(AppearanceSettings settings, byte[]? image = null)
        {
            var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            string? newImage = null;
            try
            {
                AppearanceValidation.Validate(settings);
                if (image is not null)
                {
                    CheckImage(image);
                    settings = settings with { Image = Convert.ToHexStringLower(SHA256.HashData(image)) + ".png" };
                }
                Directory.CreateDirectory(_directory);
                if (image is not null)
                {
                    var path = ImagePath(settings.Image!);
                    if (!File.Exists(path)) { File.WriteAllBytes(path, image); newImage = path; }
                    else if (!File.ReadAllBytes(path).SequenceEqual(image)) throw new ArgumentException("Existing managed image is corrupt; choose another image or restore your backup.");
                }
                if (settings.Image is not null && !File.Exists(ImagePath(settings.Image))) throw new ArgumentException("The selected managed image is missing; choose it again.");
                var json = JsonSerializer.Serialize(settings, JsonOptions);
                _ = AppearanceValidation.ParseSettings(json);
                File.WriteAllText(temporary, json);
                File.Move(temporary, _path, true);
                Current = AppearanceValidation.ParseSettings(json);
                LastError = null;
                newImage = null;
                RemoveUnusedImages();
                return true;
            }
            catch (Exception ex) when (IsRecoverable(ex)) { LastError = "Appearance was not saved; the previous settings are intact. " + ex.Message; return false; }
            finally { DeleteTemporary(temporary); if (newImage is not null) DeleteTemporary(newImage); }
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
                    if (name != Current.Image && name.Length == 68 && name.AsSpan(0, 64).ToString().All(Uri.IsHexDigit)) DeleteTemporary(path);
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
