using System.Windows;
using System.Windows.Controls;
using Heimdall.Application;
using Heimdall.Dcs;
using Heimdall.Domain;
using Heimdall.Infrastructure;
using Heimdall.App.Features.InputTest;
using Heimdall.App;

namespace Heimdall.App.Smoke
{
    /// <summary>Checks that sample-only access cannot unlock an ordinary Free workspace or real input.</summary>
    public static class FreeTrainingScopeSmoke
    {
        #region BEGIN Public
        /// <summary>Exercises scope ownership, fictional identities, direct handlers and expired sessions.</summary>
        public static void Verify()
        {
            using var environment = new TrainingEnvironment();
            environment.InitializeAsync().GetAwaiter().GetResult();
            var sample = NewModuleWorkspaceFactory.Create("TRAINING", "Training", environment.Defaults, environment.Hardware);
            environment.RequireWorkspace(sample);
            Reject(() => EditionAccess.RequireWorkspace(sample), "reserved");
            var foreign = new SqliteEditorWorkspaceRepository("Data Source=:memory:");
            Reject(() => environment.RequireRepository(foreign), "isolated repository");
            Reject(() => environment.RequireSources(environment.Repository, environment.Defaults,
                new Dictionary<string, string>(environment.Hardware)), "sample sources");
            var config = sample.Configuration;
            Reject(() => environment.RequireWorkspace(sample with { Configuration = new(
                new(config.Module.Id, "REAL-AIRCRAFT", config.Module.DisplayName), config.Profiles,
                config.Modifiers, config.Bindings, config.Provenance) }), "fictional");
            var realProfiles = config.Profiles.Select(profile => new DeviceProfile(profile.Id, profile.DeviceId,
                profile.Instance, profile.Role, "Real controller {23456789-1111-2222-3333-444455556666}.diff.lua"));
            Reject(() => environment.RequireWorkspace(sample with { Configuration = new(
                config.Module, realProfiles, config.Modifiers, config.Bindings, config.Provenance) }), "fictional");
            Reject(() => environment.CreateInputSource(0, [Guid.NewGuid()]), "physical controllers");
            // A caller-supplied factory alone is never permission to use the tester in Free.
            var factoryCalled = false;
            var ordinary = new ModuleWorkspaceWindow(new ModuleWorkspaceViewModel(sample), repository: environment.Repository,
                inputSourceFactory: (_, _) => { factoryCalled = true; return new TrainingInputSource(); });
            ordinary.Show(); ThemeControlSmoke.Pump(ordinary);
            try
            {
                if (((ContentControl)ordinary.FindName("FeatureContent")!).IsEnabled)
                    throw new InvalidOperationException("A training module identity unlocked an ordinary Free window.");
                using var input = new InputTestView();
                input.Initialize(new WindowContext(ordinary));
                ((Button)input.FindName("StartInputTestButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (factoryCalled || !((TextBlock)input.FindName("InputTestStatus")).Text.Contains("reserved", StringComparison.Ordinal))
                    throw new InvalidOperationException("An input factory bypassed ordinary Free restrictions.");
            }
            finally { ordinary.Close(); }
            using (var simulated = environment.CreateInputSource(0, [TrainingDiscovery.Unit.PhysicalGuid]))
                if (simulated.SupportedInputs.Keys.Single() != TrainingDiscovery.Unit.PhysicalGuid)
                    throw new InvalidOperationException("Training acquired an unexpected controller.");
            environment.Dispose();
            Reject(() => environment.RequireWorkspace(sample), "active session");
            Reject(() => environment.CreateInputSource(0, [TrainingDiscovery.Unit.PhysicalGuid]), "active session");
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void Reject(Action action, string expected)
        {
            try { action(); }
            catch (InvalidOperationException ex) when (ex.Message.Contains(expected, StringComparison.Ordinal)) { return; }
            throw new InvalidOperationException("Expected blocked training operation: " + expected);
        }
        #endregion END private helpers
    }
}
