using Heimdall.Application.Interfaces;
using Heimdall.Application.Validation;
using Heimdall.Dcs;
using Heimdall.Domain.MultiSim;
using Heimdall.EliteDangerous;
using Heimdall.Squadrons;
using Heimdall.WarThunder;

namespace Heimdall.Tests
{
    /// <summary>Supplies independent native examples through the mandatory adapter contract.</summary>
    internal static class ProfileValidationFixture
    {
        #region BEGIN Public
        internal static (ISimProfileBackend Backend, ProfileDocument Profile) Open(string id)
        {
            ISimProfileBackend backend = id switch
            {
                "dcs" => new DcsSimProfileBackend(), "elite-dangerous" => new EliteDangerousProfileBackend(),
                "star-wars-squadrons" => new SquadronsProfileBackend(), _ => new WarThunderProfileBackend()
            };
            var sources = id switch
            {
                "dcs" => new Dictionary<string, string> { ["Config/Input/Test/joystick/Stick.diff.lua"] = """local diff = { ["keyDiffs"] = { ["one"] = { ["name"] = "One", ["added"] = { [1] = { ["key"] = "JOY_BTN1" } } } } } return diff""" },
                "elite-dangerous" => new Dictionary<string, string> { ["Synthetic.4.2.binds"] = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "EliteDangerous", "Synthetic.4.2.binds")) },
                "star-wars-squadrons" => new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced")) },
                _ => new Dictionary<string, string> { ["Test controls.blk"] = WarThunderBackendFixture.Source }
            };
            return (backend, backend.Import(id == "dcs" ? "Test" : "controls", sources));
        }
        internal static ProfileValidationDefinition Rules(params ProfileValidationRule[] rules) => new(1, "war-thunder", 1, 1, rules, []);
        #endregion END Public
    }
}
