namespace Dcs.Ipia.Application
{
    /// <summary>Serializes atomic workflow actions with explicit reading and cancellation boundaries. Call from one UI context.</summary>
    public sealed class TrainingRunner : IAsyncDisposable
    {
        private readonly TrainingScenario _scenario;
        private readonly Func<TrainingStep, Task> _execute;
        private readonly CancellationTokenSource _pacing = new();
        private CancellationTokenSource? _delay;
        private Task? _running;
        private bool _busy;
        private bool _pause;
        private bool _stop;
        private double _speed = 1;

        /// <summary>Creates playback without executing or changing scenario data.</summary>
        public TrainingRunner(TrainingScenario scenario, Func<TrainingStep, Task> execute)
        {
            scenario.Validate();
            _scenario = scenario;
            _execute = execute;
        }
        #region BEGIN Public
        /// <summary>Occurs when progress, narration or playback state changes.</summary>
        public event EventHandler? Changed;
        /// <summary>Gets the current lifecycle state.</summary>
        public TrainingState State { get; private set; } = TrainingState.Ready;
        /// <summary>Gets the number of successfully completed actions.</summary>
        public int CompletedSteps { get; private set; }
        /// <summary>Gets the executing or most recently completed step.</summary>
        public TrainingStep? CurrentStep { get; private set; }
        /// <summary>Gets recovery context after an action or reset fails.</summary>
        public string? Error { get; private set; }
        /// <summary>Gets whether an action or pacing interval still owns the runner.</summary>
        public bool IsBusy => _busy;
        /// <summary>Gets or sets narration pacing; changes apply to the next pacing interval.</summary>
        public double Speed
        {
            get => _speed;
            set
            {
                if (!double.IsFinite(value) || value < 0.25 || value > 4) throw new ArgumentOutOfRangeException(nameof(value));
                _speed = value;
            }
        }
        /// <summary>Continues until paused, stopped, failed or a reading checkpoint is reached.</summary>
        public Task PlayAsync() => Start(false);
        /// <summary>Executes exactly one next action, retaining its explanation afterward.</summary>
        public Task NextAsync() => Start(true);
        /// <summary>Requests a pause after the current atomic action, cancelling only the pacing interval.</summary>
        public void Pause()
        {
            _pause = true;
            _delay?.Cancel();
            if (State == TrainingState.Playing) SetState(TrainingState.Paused);
        }
        /// <summary>Prevents subsequent actions and waits until active work safely settles.</summary>
        public async Task StopAsync()
        {
            _stop = true;
            _delay?.Cancel();
            SetState(TrainingState.Stopping);
            if (_running is not null) await _running;
            SetState(TrainingState.Stopped);
        }
        /// <summary>Stops playback before rebuilding the isolated environment and resetting progress.</summary>
        public async Task RestartAsync(Func<Task> reset)
        {
            await StopAsync();
            try
            {
                await reset();
                CompletedSteps = 0;
                CurrentStep = null;
                Error = null;
                _stop = false;
                SetState(TrainingState.Ready);
            }
            catch (Exception ex)
            {
                Error = "Training reset failed: " + ex.Message + " Exit or restart to retry.";
                SetState(TrainingState.Failed);
            }
        }
        /// <summary>Quiesces playback and releases pacing resources.</summary>
        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            _delay?.Dispose();
            _pacing.Dispose();
        }
        #endregion END Public
        #region BEGIN private helpers
        private Task Start(bool single)
        {
            if (_busy || _stop || State is TrainingState.Completed or TrainingState.Failed) return Task.CompletedTask;
            _busy = true;
            _pause = false;
            _running = RunAsync(single);
            return _running;
        }
        private async Task RunAsync(bool single)
        {
            try
            {
                SetState(TrainingState.Playing);
                while (!_stop && !_pause && CompletedSteps < _scenario.Steps.Count)
                {
                    CurrentStep = _scenario.Steps[CompletedSteps];
                    Changed?.Invoke(this, EventArgs.Empty);
                    await _execute(CurrentStep);
                    CompletedSteps++;
                    Changed?.Invoke(this, EventArgs.Empty);
                    if (_stop) break;
                    if (CompletedSteps == _scenario.Steps.Count) { SetState(TrainingState.Completed); break; }
                    if (single || _pause || CurrentStep.Checkpoint) { SetState(TrainingState.Paused); break; }
                    _delay?.Dispose();
                    _delay = CancellationTokenSource.CreateLinkedTokenSource(_pacing.Token);
                    try { await Task.Delay(TimeSpan.FromSeconds(4 / Speed), _delay.Token); }
                    catch (OperationCanceledException) when (_pause || _stop) { }
                }
            }
            catch (Exception ex)
            {
                Error = $"Step {CurrentStep?.Id}: {ex.Message} Restart the scenario or exit training.";
                SetState(TrainingState.Failed);
            }
            finally
            {
                _busy = false;
                if (!_stop && State == TrainingState.Playing) SetState(TrainingState.Paused);
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
        private void SetState(TrainingState state)
        {
            State = state;
            Changed?.Invoke(this, EventArgs.Empty);
        }
        #endregion END private helpers
    }
}
