using DaJet.Runtime;
using System.Text;

namespace DaJet.Flow.Script
{
    public sealed class Runner : ISourceBlock
    {
        private readonly IPipeline _pipeline;
        private readonly IProcessor _processor;
        private readonly ScriptOptions _options;
        public Runner(ScriptOptions options, IPipeline pipeline)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));

            string script = Path.Combine(AppContext.BaseDirectory, _options.Script);

            using (StreamReader reader = new(script, Encoding.UTF8))
            {
                script = reader.ReadToEnd();
            }

            Dictionary<string, object> parameters = _options.Parameters is null ? new() : _options.Parameters;

            if (!StreamFactory.TryCreateStream(in script, in parameters, out _processor, out string error))
            {
                throw new Exception(error);
            }
        }
        public void Execute()
        {
            _pipeline.UpdateMonitorStatus("Executing...");

            string message = string.Empty;

            _processor.Process();

            if (_processor is RootProcessor root && root.ReturnValue is not null)
            {
                message = root.ReturnValue.ToString();
            }

            _pipeline.UpdateMonitorStatus(message);
        }
        public void Dispose()
        {
            _processor.Dispose();
            
            _pipeline.UpdateMonitorStatus("Disposed");
        }
    }
}