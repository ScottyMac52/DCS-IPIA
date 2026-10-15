namespace Dcs.Ipia.Application
{
    /// <summary>Observable lifecycle of a training sequence.</summary>
    public enum TrainingState { Ready, Playing, Paused, Completed, Failed, Stopping, Stopped }
}
