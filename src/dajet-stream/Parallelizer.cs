using DaJet.Data;
using DaJet.Scripting.Model;

namespace DaJet.Stream
{
    public sealed class Parallelizer : IProcessor
    {
        private IProcessor _next;
        private readonly StreamScope _scope;
        private readonly int _maxdop;
        private readonly string _options;
        private readonly string _iterator;
        private readonly List<string> _closure;
        private static int GetMaxDopValue(in ForEachStatement statement)
        {
            int maxdop = statement.DegreeOfParallelism;

            if (maxdop == 1)
            {
                return maxdop;
            }
            else
            {
                int cpu_cores = Environment.ProcessorCount;

                if (maxdop == 0)
                {
                    maxdop = cpu_cores;
                }
                else if (maxdop < 0)
                {
                    maxdop = cpu_cores - 1;
                }
                else if (maxdop > 1)
                {
                    maxdop = Math.Min(cpu_cores, maxdop);
                }
            }

            return maxdop;
        }
        public Parallelizer(in StreamScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not ForEachStatement statement)
            {
                throw new ArgumentException(nameof(ForEachStatement));
            }

            StreamFactory.ConfigureIteratorSchema(in _scope, out _options, out _iterator);

            _maxdop = GetMaxDopValue(in statement);
            
            _closure = StreamFactory.GetClosureVariables(in _scope);

            _ = StreamFactory.CreateStream(in _scope); // transpile and cache SQL statements 
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

            foreach (string variable in _closure)
            {
                if (_scope.TryGetValue(in variable, out object value))
                {
                    clone.Variables.Add(variable, value);
                }
            }

            _ = clone.TrySetValue(_options, options);

            IProcessor stream = StreamFactory.CreateStream(in clone);

            stream?.Process();
        }
    }
}