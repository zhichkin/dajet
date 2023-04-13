namespace DaJet.Flow.Model
{
    public enum PipelineState
    {
        None, // broken pipeline
        Working,
        Stopped,
        Sleeping,
        Stopping
    }
}