using DaJet.Data;
using DaJet.Scripting.Model;
using System.Threading.Tasks;

namespace DaJet.Runtime
{
    public sealed class WaitProcessor : IProcessor
    {
        private IProcessor _next;
        private readonly WaitKind _kind;
        private readonly ScriptScope _scope;
        private readonly WaitStatement _statement;
        private readonly string _task;
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

            if (_kind == WaitKind.Any)
            {
                _task = GetTaskObjectReference();
            }

            _tasks = GetTaskArrayReference();
        }
        public void Dispose() { _next?.Dispose(); }
        public void Synchronize() { _next?.Synchronize(); }
        public void LinkTo(in IProcessor next) { _next = next; }
        public void Process()
        {
            WaitForTasksToComplete();

            _next?.Process();
        }
        private string GetTaskObjectReference()
        {
            if (_statement.Task is not VariableReference variable)
            {
                throw new InvalidOperationException($"[WAIT] task object is not variable");
            }

            string identifier = _statement.Task.Identifier;

            if (!_scope.TryGetValue(in identifier, out object value))
            {
                throw new InvalidOperationException($"[WAIT] task object variable {identifier} is not found");
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
        private DataObject CreateTaskObject()
        {
            DataObject task = new(8);
            task.SetValue("Id", 0);
            task.SetValue("Task", null);
            task.SetValue("Result", null);
            task.SetValue("Status", string.Empty);
            task.SetValue("IsFaulted", false);
            task.SetValue("IsCanceled", false);
            task.SetValue("IsCompleted", false);
            task.SetValue("IsSucceeded", false);
            return task;
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

                    if (job.Exception is null)
                    {
                        task.SetValue("Result", null);
                    }
                    else
                    {
                        if (job.Exception.InnerException is not ReturnException _return)
                        {
                            task.SetValue("Result", job.Exception.Message);
                        }
                        else
                        {
                            task.SetValue("Result", _return.Value);

                            if (job.IsCompleted)
                            {
                                task.SetValue("IsFaulted", false);
                                task.SetValue("IsSucceeded", true);
                            }
                        }
                    }
                }
            }
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
            try
            {
                Task.WaitAll(tasks);
            }
            catch { /* DO NOTHING */ }

            foreach (DataObject task in _tasks)
            {
                UpdateTaskObjectValues(in task);
            }
        }
        private void WaitForAnyTasksToComplete(in Task[] tasks)
        {
            int index = -1;

            try
            {
                index = Task.WaitAny(tasks);
            }
            catch { /* DO NOTHING */ }

            DataObject task = _tasks[index];
            UpdateTaskObjectValues(in task);
            _tasks.RemoveAt(index);

            if (!_scope.TrySetValue(_task, task))
            {
                throw new InvalidOperationException($"[WAIT] Error setting task object variable {_task}");
            }
        }
    }
}