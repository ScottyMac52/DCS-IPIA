namespace Heimdall.Infrastructure
{
    /// <summary>Uses HEIMDALL storage for new installs while resolving existing legacy data without moving it.</summary>
    public static class ProductDataPaths
    {
        #region BEGIN Public
        /// <summary>Finds the active production root; an existing database wins over a newly created log directory.</summary>
        public static string ProductionDirectory(string localData)
        {
            var current = Path.Combine(localData, "HEIMDALL");
            var legacy = Path.Combine(localData, "DCS-IPIA");
            return File.Exists(Path.Combine(current, "ipia.db")) || !Directory.Exists(legacy) ? current : legacy;
        }
        /// <summary>Finds an existing named test profile before choosing a new HEIMDALL test-profile directory.</summary>
        public static string TestDirectory(string localData, string name)
        {
            var current = Path.Combine(localData, "HEIMDALL-TestProfiles");
            var legacy = Path.Combine(localData, "DCS-IPIA-TestProfiles");
            if (Directory.Exists(Path.Combine(current, name))) return Path.Combine(current, name);
            if (Directory.Exists(Path.Combine(legacy, name)) || (!Directory.Exists(current) && Directory.Exists(legacy)))
                return Path.Combine(legacy, name);
            return Path.Combine(current, name);
        }
        /// <summary>Finds the accepted owner license independently of profile backups and resets.</summary>
        public static string OwnerLicenseFile(string localData) => ExistingFile(
            Path.Combine(localData, "VyperIndustries", "HEIMDALL-Licensing", "owner-license.json"),
            Path.Combine(localData, "VyperIndustries", "DCS-IPIA-Licensing", "owner-license.json"));
        /// <summary>Retains existing global layout preferences without copying or overwriting them.</summary>
        public static string LayoutFile(string localData) => ExistingFile(
            Path.Combine(localData, "HEIMDALL", "workspace-layout.json"),
            Path.Combine(localData, "DCS-IPIA", "workspace-layout.json"));
        #endregion END Public
        #region BEGIN private helpers
        private static string ExistingFile(string current, string legacy) =>
            File.Exists(current) || !File.Exists(legacy) ? current : legacy;
        #endregion END private helpers
    }
}
