namespace DaJet.Flow.Model
{
    public enum PipelineState
    {
        None = -1, // broken pipeline
        Idle = 0,
        Starting = 1,
        Working = 2,
        Disposing = 3,
        Sleeping = 4
    }
}