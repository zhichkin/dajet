using DaJet.Data;
using DaJet.Scripting.Model;

namespace DaJet.Runtime
{
    public sealed class WaitProcessor : IProcessor
    {
        private IProcessor _next;
        private readonly WaitKind _kind;
        private readonly ScriptScope _scope;
        private readonly WaitStatement _statement;
        private readonly int _timeout;
        private readonly string _result;
        private readonly List<DataObject> _tasks;
        public WaitProcessor(in ScriptScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not WaitStatement statement)
            {
                throw new ArgumentException(nameof(ForStatement));
            }

            _statement = statement;
            _kind = _statement.Kind;
            _timeout = _statement.Timeout;
            _tasks = GetTaskArrayReference();

            if (_kind == WaitKind.Any)
            {
                _result = GetResultVariableIdentifier();
            }
            else if (_kind == WaitKind.All && _timeout > 0)
            {
                _result = GetResultVariableIdentifier();
            }
        }
        public void Dispose() { _next?.Dispose(); }
        public void Synchronize() { _next?.Synchronize(); }
        public void LinkTo(in IProcessor next) { _next = next; }
        public void Process()
        {
            if (_tasks.Count > 0)
            {
                WaitForTasksToComplete();
            }

            _next?.Process();
        }
        private string GetResultVariableIdentifier()
        {
            if (_statement.Result is not VariableReference variable)
            {
                throw new InvalidOperationException($"[WAIT] result variable is not defined");
            }

            string identifier = _statement.Result.Identifier;

            if (!_scope.TryGetValue(in identifier, out _))
            {
                throw new InvalidOperationException($"[WAIT] result variable {identifier} is not found");
            }

            return identifier;
        }
        private List<DataObject> GetTaskArrayReference()
        {
            if (_statement.Tasks is not VariableReference variable)
            {
                throw new InvalidOperationException($"[WAIT] task array is not variable");
            }

            string identifier = _statement.Tasks.Identifier;

            if (!_scope.TryGetValue(in identifier, out object value))
            {
                throw new InvalidOperationException($"[WAIT] task array variable {identifier} is not found");
            }

            if (value is not List<DataObject> tasks)
            {
                tasks = new List<DataObject>();

                if (!_scope.TrySetValue(in identifier, tasks))
                {
                    throw new InvalidOperationException($"[WAIT] Error setting task array variable {identifier}");
                }
            }

            return tasks;
        }
        
        private void WaitForTasksToComplete()
        {
            Task[] tasks = new Task[_tasks.Count];

            for (int index = 0; index < _tasks.Count; index++)
            {
                DataObject task = _tasks[index];

                if (task.TryGetValue("Task", out object value))
                {
                    if (value is Task job)
                    {
                        tasks[index] = job;
                    }
                }
            }

            if (_kind == WaitKind.All)
            {
                WaitForAllTasksToComplete(in tasks);
            }
            else
            {
                WaitForAnyTasksToComplete(in tasks);
            }
        }
        private void WaitForAllTasksToComplete(in Task[] tasks)
        {
            bool completed = true;

            try
            {
                if (_timeout > 0)
                {
                    completed = Task.WaitAll(tasks, TimeSpan.FromSeconds(_timeout));
                }
                else
                {
                    Task.WaitAll(tasks);
                }
            }
            catch { /* DO NOTHING */ }

            foreach (DataObject task in _tasks)
            {
                UpdateTaskObjectValues(in task);
            }

            if (!string.IsNullOrEmpty(_result))
            {
                if (!_scope.TrySetValue(_result, completed))
                {
                    throw new InvalidOperationException($"[WAIT] Error setting completed variable {_result}");
                }
            }
        }
        private void WaitForAnyTasksToComplete(in Task[] tasks)
        {
            int index = -1;
            DataObject task = null;

            try
            {
                if (_timeout > 0)
                {
                    index = Task.WaitAny(tasks, TimeSpan.FromSeconds(_timeout));
                }
                else
                {
                    index = Task.WaitAny(tasks);
                }
            }
            catch { /* DO NOTHING */ }

            if (index >= 0)
            {
                task = _tasks[index];
                UpdateTaskObjectValues(in task);
                _tasks.RemoveAt(index);
            }

            if (!_scope.TrySetValue(_result, task))
            {
                throw new InvalidOperationException($"[WAIT] Error setting task object variable {_result}");
            }
        }
        private void UpdateTaskObjectValues(in DataObject task)
        {
            if (task.TryGetValue("Task", out object value))
            {
                if (value is Task job)
                {
                    task.SetValue("Status", job.Status.ToString());
                    task.SetValue("IsFaulted", job.IsFaulted);
                    task.SetValue("IsCanceled", job.IsCanceled);
                    task.SetValue("IsCompleted", job.IsCompleted);
                    task.SetValue("IsSucceeded", job.IsCompletedSuccessfully);

                    if (job.Exception is null) // no return value
                    {
                        task.SetValue("Result", null);
                    }
                    else
                    {
                        if (job.Exception.InnerException is not ReturnException _return)
                        {
                            task.SetValue("Result", job.Exception.Message); // faulted
                        }
                        else
                        {
                            task.SetValue("Result", _return.Value); // success

                            if (job.IsCompleted)
                            {
                                task.SetValue("Status", TaskStatus.RanToCompletion.ToString());
                                task.SetValue("IsFaulted", false);
                                task.SetValue("IsSucceeded", true);
                            }
                        }
                    }
                }
            }
        }
    }
}