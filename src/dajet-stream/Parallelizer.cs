using DaJet.Data;
using DaJet.Metadata;
using DaJet.Scripting;
using DaJet.Scripting.Model;

namespace DaJet.Stream
{
    internal sealed class Parallelizer : ProcessorBase
    {
        private int _maxdop = 1;
        private PipelineBuilder _builder;
        internal Parallelizer(in IMetadataProvider context, in SqlStatement statement, in Dictionary<string, object> parameters)
            : base(in context, in statement, in parameters) { }
        public override void Synchronize() { /* do nothing */ }
        internal void SetPipelineBuilder(in PipelineBuilder builder)
        {
            _builder = builder;
        }
        public override void Process()
        {
            if (_statement.Node is not ForEachStatement node)
            {
                throw new InvalidOperationException();
            }

            _maxdop = node.DegreeOfParallelism;
            _arrayName = node.Iterator.Identifier;
            _objectName = node.Variable.Identifier;

            if (!_parameters.TryGetValue(_arrayName, out object value))
            {
                throw new InvalidOperationException();
            }

            if (value is not List<DataObject> iterator)
            {
                throw new InvalidOperationException();
            }

            if (iterator.Count > 0)
            {
                Parallelize(in iterator);
            }

            _next?.Process();
        }
        private void Parallelize(in List<DataObject> iterator)
        {
            int cpu_cores = Environment.ProcessorCount;

            if (_maxdop == 0)
            {
                _maxdop = cpu_cores;
            }
            else if (_maxdop > 1)
            {
                _maxdop = Math.Min(cpu_cores, _maxdop);
            }
            else if (_maxdop < 0)
            {
                _maxdop = cpu_cores - 1;
            }

            Console.WriteLine($"MAXDOP = {_maxdop}");

            ParallelOptions options = new()
            {
                MaxDegreeOfParallelism = _maxdop
            };

            ParallelLoopResult result = Parallel.ForEach(iterator, options, ProcessInParallel);
        }
        private void ProcessInParallel(DataObject variable)
        {
            Console.WriteLine($"Thread: {Environment.CurrentManagedThreadId}");

            Dictionary<string, object> parameters = new();

            foreach (var parameter in _parameters)
            {
                parameters.Add(parameter.Key, parameter.Value);
            }

            parameters.Add(_objectName, variable);

            List<IProcessor> processors = _builder.Build(in parameters);

            processors[0].Process();
        }
    }
}