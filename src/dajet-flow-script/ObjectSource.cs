//using DaJet.Data;
//using DaJet.Runtime;
//using System.Text;

namespace DaJet.Flow.Script
{
    //public sealed class ObjectSource : ISourceBlock, IOutputBlock<DataObject>
    //{
    //    private IInputBlock<DataObject> _next;
    //    public void LinkTo(in IInputBlock<DataObject> next)
    //    {
    //        _next = next;
    //    }
    //    private readonly IPipeline _pipeline;
    //    private readonly IProcessor _processor;
    //    private readonly ScriptOptions _options;
    //    public ObjectSource(ScriptOptions options, IPipeline pipeline)
    //    {
    //        _options = options ?? throw new ArgumentNullException(nameof(options));
    //        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));

    //        string script = Path.Combine(AppContext.BaseDirectory, _options.Script);

    //        using (StreamReader reader = new(script, Encoding.UTF8))
    //        {
    //            script = reader.ReadToEnd();
    //        }

    //        Dictionary<string, object> parameters = _options.Parameters is null ? new() : _options.Parameters;

    //        if (!StreamFactory.TryCreateStream(in script, in parameters, out _processor, out string error))
    //        {
    //            throw new Exception(error);
    //        }
    //    }
    //    public void Execute()
    //    {
    //        _pipeline.UpdateMonitorStatus($"Executing...");

    //        DataObject output = null;

    //        _processor.Process();

    //        if (_processor is RootProcessor root)
    //        {
    //            output = root.ReturnValue as DataObject;
    //        }

    //        _next?.Process(in output);

    //        //DataObjectJsonConverter() ?

    //        string message = output is null ? string.Empty : output.GetValue(0).ToString();

    //        _pipeline.UpdateMonitorStatus($"Output: {message}");
    //    }
    //    public void Dispose()
    //    {
    //        _processor.Dispose();
    //    }
    //}
}