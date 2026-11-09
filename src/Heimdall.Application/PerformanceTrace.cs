namespace Heimdall.Application
{
    /// <summary>Connects compile-time profiling hooks to one bounded process-local capture.</summary>
    public static class PerformanceTrace
    {
        private static readonly PerformanceCapture Capture = new();

        #region BEGIN Public
        /// <summary>Starts an inclusive timer with a fixed operation tag and a simulator inferred from its owning type.</summary>
        public static PerformanceScope Measure(string operation, Type owner)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(operation);
            ArgumentNullException.ThrowIfNull(owner);
            return new(Capture, operation, Component(owner));
        }
        /// <summary>Records dispatcher delay or another already measured duration under fixed tags.</summary>
        public static void Record(string operation, double durationMs) => Capture.Record(operation, "Shared", durationMs);
        /// <summary>Gets an independent snapshot of retained measurements.</summary>
        public static PerformanceSample[] Snapshot() => Capture.Snapshot();
        /// <summary>Gets the number of measurements omitted after the fixed capacity was reached.</summary>
        public static long Dropped => Capture.Dropped;
        #endregion END Public

        #region BEGIN private helpers
        private static string Component(Type owner)
        {
            var name = owner.Name;
            if (name.StartsWith("Dcs", StringComparison.Ordinal)) return "DCS";
            if (name.StartsWith("Elite", StringComparison.Ordinal)) return "Elite";
            if (name.StartsWith("Squadrons", StringComparison.Ordinal)) return "Squadrons";
            if (name.StartsWith("WarThunder", StringComparison.Ordinal)) return "WarThunder";
            return "Shared";
        }
        #endregion END private helpers
    }
}
