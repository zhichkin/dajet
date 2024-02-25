using DaJet.Data;
using DaJet.Scripting;
using DaJet.Scripting.Model;

namespace DaJet.Stream
{
    public sealed class Parallelizer : IProcessor
    {
        private IProcessor _next;
        private readonly ScriptScope _scope;
        private int _maxdop = 1;
        private readonly string _arrayName;
        private readonly string _objectName;
        private readonly Dictionary<string, object> _context;
        private List<DataObject> _iterator;
        private readonly List<IProcessor> _streams = new();
        public Parallelizer(in ScriptScope scope)
        {
            if (scope.Owner is not ForEachStatement statement)
            {
                throw new InvalidOperationException();
            }

            ScriptScope parent = scope.Ancestor<ForEachStatement>();
            parent ??= scope.Ancestor<ScriptModel>();

            _context = parent.Variables; //TODO: ???

            _maxdop = statement.DegreeOfParallelism;
            _arrayName = statement.Iterator.Identifier;
            _objectName = statement.Variable.Identifier;
        }
        public void LinkTo(in IProcessor next) { _next = next; }
        public void Synchronize() { throw new NotImplementedException(); }
        public void Dispose() { throw new NotImplementedException(); }
        public void Process()
        {
            if (!_context.TryGetValue(_arrayName, out object value))
            {
                throw new InvalidOperationException();
            }

            _iterator = value as List<DataObject>;

            if (_iterator is null)
            {
                throw new InvalidOperationException();
            }

            foreach (DataObject options in _iterator)
            {
                Dictionary<string, object> context = new();

                foreach (var variable in _context)
                {
                    context.Add(variable.Key, variable.Value);
                }

                context.Add(_objectName, options);

                IProcessor stream = StreamFactory.Create(_scope.Children);

                _streams.Add(stream);
            }

            if (_streams.Count > 0)
            {
                Parallelize();
            }

            _next?.Process();
        }
        private void Parallelize()
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

            ParallelLoopResult result = Parallel.ForEach(_streams, options, ProcessInParallel);
        }
        private void ProcessInParallel(IProcessor stream)
        {
            Console.WriteLine($"Thread: {Environment.CurrentManagedThreadId}");

            stream.Process();
        }
    }
}