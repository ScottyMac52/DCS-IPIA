using Microsoft.Extensions.Logging;

namespace DcsIpia.Presentation
{
    /// <summary>Cached, strongly typed diagnostic messages with stable event identities.</summary>
    internal static class ApplicationLog
    {
        internal static readonly Action<ILogger, string, Guid, string, string, Exception?> ModuleOutputFailure =
            LoggerMessage.Define<string, Guid, string, string>(LogLevel.Error, new EventId(1015, nameof(ModuleOutputFailure)),
                "Module operation {Operation} failed for {ModuleId} {DcsId}; package version {PackageVersion}");
        internal static readonly Action<ILogger, string, Exception?> Starting = LoggerMessage.Define<string>(
            LogLevel.Information, new EventId(1000, nameof(Starting)), "Starting IPIA {Version}");
        internal static readonly Action<ILogger, Exception?> Terminated = LoggerMessage.Define(
            LogLevel.Critical, new EventId(1001, nameof(Terminated)), "IPIA terminated unexpectedly");
        internal static readonly Action<ILogger, Exception?> DispatcherFailure = LoggerMessage.Define(
            LogLevel.Critical, new EventId(1002, nameof(DispatcherFailure)), "Unhandled WPF dispatcher exception");
        internal static readonly Action<ILogger, Exception?> InvalidProfile = LoggerMessage.Define(
            LogLevel.Error, new EventId(1003, nameof(InvalidProfile)), "Invalid IPIA profile arguments");
        internal static readonly Action<ILogger, string, Exception?> DataLockFailure = LoggerMessage.Define<string>(
            LogLevel.Error, new EventId(1004, nameof(DataLockFailure)), "Unable to acquire user-data lock {UserFolder}");
        internal static readonly Action<ILogger, string, Exception?> DataRecoveryFailure = LoggerMessage.Define<string>(
            LogLevel.Error, new EventId(1005, nameof(DataRecoveryFailure)), "Unable to recover or migrate local data {UserFolder}");
        internal static readonly Action<ILogger, string, Exception?> ReleaseSmokeFailure = LoggerMessage.Define<string>(
            LogLevel.Error, new EventId(1006, nameof(ReleaseSmokeFailure)), "Published application smoke failed {UserFolder}");
        internal static readonly Action<ILogger, Exception?> NewModuleFailure = LoggerMessage.Define(
            LogLevel.Error, new EventId(1007, nameof(NewModuleFailure)), "Unable to open or complete new-module creation");
        internal static readonly Action<ILogger, Exception?> InventoryFailure = LoggerMessage.Define(
            LogLevel.Error, new EventId(1008, nameof(InventoryFailure)), "Unable to read physical hardware inventory");
        internal static readonly Action<ILogger, Exception?> ControllerDiscoveryFailure = LoggerMessage.Define(
            LogLevel.Error, new EventId(1009, nameof(ControllerDiscoveryFailure)), "Unable to discover connected controllers");
        internal static readonly Action<ILogger, string, Exception?> CapturedDiscoveryFailure = LoggerMessage.Define<string>(
            LogLevel.Error, new EventId(1010, nameof(CapturedDiscoveryFailure)), "Unable to discover captured devices in {SavedGamesRoot}");
        internal static readonly Action<ILogger, int, string, Exception?> DevicesDiscovered = LoggerMessage.Define<int, string>(
            LogLevel.Debug, new EventId(1011, nameof(DevicesDiscovered)), "Discovered {Count} physical units in {SavedGamesRoot}");
        internal static readonly Action<ILogger, string, Guid?, string?, string?, Exception?> AddUnitFailure =
            LoggerMessage.Define<string, Guid?, string?, string?>(LogLevel.Error, new EventId(1012, nameof(AddUnitFailure)),
                "Unable to add unit {FileName} GUID {PhysicalGuid} hardware {HardwareId} baseline {BaselineFileName}");
        internal static readonly Action<ILogger, string, Guid?, string, string?, Exception?> ApplyingUnit =
            LoggerMessage.Define<string, Guid?, string, string?>(LogLevel.Debug, new EventId(1013, nameof(ApplyingUnit)),
                "Applying unit {FileName} GUID {PhysicalGuid} hardware {HardwareId} baseline {BaselineFileName}");
        internal static readonly Action<ILogger, string, string, bool, int, object, object?, Exception?> CreateModuleFailure =
            LoggerMessage.Define<string, string, bool, int, object, object?>(LogLevel.Error, new EventId(1014, nameof(CreateModuleFailure)),
                "Unable to create module {DcsId} {DisplayName}; empty defaults {EmptyDefaults}; " +
                "revision {Revision}; selected units {@SelectedUnits}; staged profiles {@Profiles}");
    }
}

