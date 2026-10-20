using Dcs.Ipia.Application;

namespace Dcs.Ipia.Infrastructure
{
    /// <summary>Stores an authenticated owner file outside profile backups; never persists invalid imports.</summary>
    public sealed class FileOwnerLicenseStore
    {
        private readonly string _path;
        private readonly OwnerLicenseVerifier _verifier;
        /// <summary>Uses a per-user licensing path and the application verification key.</summary>
        public FileOwnerLicenseStore(string path, OwnerLicenseVerifier? verifier = null)
        { _path = path; _verifier = verifier ?? new OwnerLicenseVerifier(); }
        #region BEGIN Public
        /// <summary>Gets the most recent actionable licensing diagnostic.</summary>
        public string? LastError { get; private set; }
        /// <summary>Returns Free for absent, inaccessible, malformed or unauthenticated documents.</summary>
        public EditionPolicy Load()
        {
            LastError = null;
            try
            {
                if (!File.Exists(_path)) return new EditionPolicy();
                return new EditionPolicy(ReadValidated(_path), _verifier);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
            { LastError = "Owner license unavailable; using Free Edition. " + ex.Message; return new EditionPolicy(); }
        }
        /// <summary>Authenticates before atomically replacing an accepted license; restart applies it.</summary>
        public bool Import(string source)
        {
            LastError = null; string? temporary = null;
            try
            {
                var json = ReadValidated(source);
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
                temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllText(temporary, json);
                File.Move(temporary, _path, true);
                return true;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
            { LastError = "Owner license was not installed. " + ex.Message; return false; }
            finally { if (temporary is not null && File.Exists(temporary)) File.Delete(temporary); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private string ReadValidated(string path)
        {
            using var stream = File.OpenRead(path);
            if (stream.Length > OwnerLicenseVerifier.MaximumBytes) throw new InvalidDataException("License exceeds the supported size.");
            using var reader = new StreamReader(stream);
            var buffer = new char[OwnerLicenseVerifier.MaximumBytes + 1];
            var count = reader.ReadBlock(buffer, 0, buffer.Length);
            if (count > OwnerLicenseVerifier.MaximumBytes) throw new InvalidDataException("License exceeds the supported size.");
            var json = new string(buffer, 0, count);
            if (_verifier.Verify(json) is null) throw new InvalidDataException("Choose an authentic signed HEIMDALL owner license.");
            return json;
        }
        #endregion END private helpers
    }
}
