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
        private List<DataObject> _iterator;
        public Parallelizer(in ScriptScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not ForEachStatement statement)
            {
                throw new ArgumentException(nameof(ForEachStatement));
            }

            IProcessor stream = StreamFactory.CreateStream(in _scope);

            _maxdop = statement.DegreeOfParallelism;

            //_context.MapIntoArray(in statement);
            //_context.MapIntoObject(in statement);
        }
        public void LinkTo(in IProcessor next) { _next = next; }
        public void Synchronize() { throw new NotImplementedException(); }
        public void Dispose() { throw new NotImplementedException(); }
        public void Process()
        {
            //_iterator = _context.GetIntoArray();

            //if (_iterator is null)
            //{
            //    throw new InvalidOperationException();
            //}

            //if (_iterator.Count == 0)
            //{
            //    // ???
            //}

            foreach (DataObject options in _iterator)
            {
                //StreamContext context = _context.Clone();

                //context.SetIntoObject(in options);

                //IProcessor stream = StreamFactory.CreateStream(in _scope);
            }

            if (_iterator.Count > 0)
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

            ParallelLoopResult result = Parallel.ForEach(_iterator, options, ProcessInParallel);
        }
        private void ProcessInParallel(DataObject options)
        {
            Console.WriteLine($"Thread: {Environment.CurrentManagedThreadId}");
        }
    }
}