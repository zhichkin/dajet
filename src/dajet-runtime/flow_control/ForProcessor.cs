using DaJet.Data;
using DaJet.Scripting.Model;

namespace DaJet.Runtime
{
    public sealed class ForProcessor : IProcessor
    {
        private IProcessor _next;
        private readonly int _maxdop;
        private readonly string _item;
        private readonly string _iterator;
        private readonly List<string> _closure;
        private readonly ForStatement _statement;
        private readonly ScriptScope _body;
        private readonly ScriptScope _scope;
        private readonly List<IProcessor> _streams = new();
        private static int GetMaxDopValue(in ForStatement statement)
        {
            int maxdop = statement.DegreeOfParallelism;

            if (maxdop == 1 || maxdop == int.MaxValue)
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
        public ForProcessor(in ScriptScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not ForStatement statement)
            {
                throw new ArgumentException(nameof(ForStatement));
            }

            _statement = statement;

            StreamFactory.ConfigureIteratorSchema(in _scope, out _item, out _iterator);

            _maxdop = GetMaxDopValue(in _statement);

            _body = _scope.Create(_statement.Statements); // NOTE: create child scope

            _closure = StreamFactory.GetClosureVariables(in _scope);

            _ = StreamFactory.CreateStream(in _body); //NOTE: transpile and cache SQL statements 
        }
        public void LinkTo(in IProcessor next) { _next = next; }
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
                //FileLogger.Default.Write("[Parallelizer] Nothing to process");

                return;
            }

            if (_maxdop == 1)
            {
                ProcessSingleThread(in iterator);
            }
            else if (_maxdop == int.MaxValue)
            {
                ProcessMaxDopUnbounded(in iterator);
            }
            else
            {
                Parallelize(in iterator);
            }

            _next?.Process();
        }
        private void ProcessSingleThread(in List<DataObject> iterator)
        {
            IProcessor stream = StreamFactory.CreateStream(in _body);

            foreach (DataObject item in iterator)
            {
                if (_body.TrySetValue(_item, item))
                {
                    stream?.Process();
                }
            }
        }
        private IProcessor CloneStreamTemplate(in DataObject item)
        {
            ScriptScope clone = _body.Clone();

            foreach (string variable in _closure)
            {
                if (_body.TryGetValue(in variable, out object value))
                {
                    clone.Variables.Add(variable, value);
                }
            }

            _ = clone.TrySetValue(_item, item);

            return StreamFactory.CreateStream(in clone);
        }

        private void Parallelize(in List<DataObject> iterator)
        {
            ParallelOptions options = new()
            {
                MaxDegreeOfParallelism = _maxdop
            };

            ParallelLoopResult result = Parallel.ForEach(iterator, options, ProcessInParallel);
        }
        private void ProcessInParallel(DataObject item)
        {
            IProcessor stream = CloneStreamTemplate(in item);

            stream?.Process();
        }

        private void ProcessMaxDopUnbounded(in List<DataObject> iterator)
        {
            foreach (DataObject item in iterator)
            {
                IProcessor stream = CloneStreamTemplate(in item);

                if (stream is not null)
                {
                    _ = Task.Factory.StartNew(stream.Process, TaskCreationOptions.LongRunning);

                    _streams.Add(stream);
                }
            }
        }

        public void Synchronize() { _next?.Synchronize(); }
        public void Dispose()
        {
            foreach (IProcessor stream in _streams)
            {
                try
                {
                    stream.Dispose();
                }
                catch (Exception error)
                {
                    FileLogger.Default.Write(error);
                }
            }

            _streams.Clear();

            _next?.Dispose();
        }
    }
}