using DaJet.Data;
using DaJet.Scripting.Model;

namespace DaJet.Stream
{
    public sealed class Parallelizer : IProcessor
    {
        private IProcessor _next;
        private readonly StreamScope _scope;
        private readonly int _maxdop = 1;
        private readonly string _options;
        private readonly string _iterator;
        public Parallelizer(in StreamScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not ForEachStatement statement)
            {
                throw new ArgumentException(nameof(ForEachStatement));
            }

            _maxdop = statement.DegreeOfParallelism;

            if (_maxdop == 1) { /* do nothing */ }
            else
            {
                int cpu_cores = Environment.ProcessorCount;
                if (_maxdop == 0) { _maxdop = cpu_cores; }
                else if (_maxdop < 0) { _maxdop = cpu_cores - 1; }
                else if (_maxdop > 1) { _maxdop = Math.Min(cpu_cores, _maxdop); }
            }

            _options = statement.Variable.Identifier;
            _iterator = statement.Iterator.Identifier;
        }
        public void LinkTo(in IProcessor next) { _next = next; }
        public void Synchronize() { throw new NotImplementedException(); }
        public void Dispose() { throw new NotImplementedException(); }
        public void Process()
        {
            if (!_scope.TryGetValue(in _iterator, out object value))
            {
                throw new InvalidOperationException($"Iterator {_iterator} is not found");
            }

            if (value is not List<DataObject> iterator)
            {
                throw new InvalidOperationException($"Iterator {_iterator} is not of type List<DataObject>");
            }

            if (iterator.Count == 0)
            {
                return; // nothing to process
            }

            // dynamic object schema binding - inferring schema from iterator

            if (!_scope.TryGetDeclaration(in _iterator, out _, out DeclareStatement schema))
            {
                throw new InvalidOperationException($"Declaration of {_iterator} is not found");
            }
            
            if (!_scope.TryGetDeclaration(in _options, out _, out DeclareStatement declare))
            {
                throw new InvalidOperationException($"Declaration of {_options} is not found");
            }

            declare.Type.Binding = schema.Type.Binding;

            Parallelize(in iterator);

            _next?.Process();
        }
        private void Parallelize(in List<DataObject> iterator)
        {
            Console.WriteLine($"MAXDOP = {_maxdop}");

            ParallelOptions options = new()
            {
                MaxDegreeOfParallelism = _maxdop
            };

            ParallelLoopResult result = Parallel.ForEach(iterator, options, ProcessInParallel);
        }
        private void ProcessInParallel(DataObject options)
        {
            Console.WriteLine($"Thread: {Environment.CurrentManagedThreadId}");

            StreamScope clone = _scope.Clone();

            //TODO: clone @message variable !!!

            clone.Variables.Add(_options, options); // something like closure

            IProcessor stream = StreamFactory.CreateStream(in clone);

            stream?.Process();
        }
    }
}