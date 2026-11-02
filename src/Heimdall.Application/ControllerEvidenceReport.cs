using System.Text.Json;
using System.Text.Json.Serialization;

namespace Heimdall.Application
{
    /// <summary>Exports read-only discovery evidence separately from game profiles, saved hardware and assignment drafts.</summary>
    public static class ControllerEvidenceReport
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter<PhysicalInputKind>() }
        };
        #region BEGIN Public
        /// <summary>Includes complete physical objects alongside the existing DCS compatibility projection and per-device failures.</summary>
        public static string Create(IReadOnlyList<ControllerObservation> observations, DateTimeOffset capturedAt)
        {
            ArgumentNullException.ThrowIfNull(observations);
            return JsonSerializer.Serialize(new
            {
                schemaVersion = 1, capturedAt = capturedAt.ToUniversalTime(),
                offsetMeaning = "raw DirectInput device offsets; not application-buffer offsets or game indices",
                devices = observations.Select(observation => new
                {
                    observation.Device, observation.Error,
                    physicalInputs = ControllerInputCatalog.Create(observation.Capabilities?.NativeObjects),
                    legacyControls = observation.Capabilities?.Inputs,
                    legacyUnsupported = observation.Capabilities?.Unsupported
                }).ToArray()
            }, Options);
        }
        #endregion END Public
    }
}
