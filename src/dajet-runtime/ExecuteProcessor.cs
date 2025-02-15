using DaJet.Data;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using System.Text;

namespace DaJet.Runtime
{
    public sealed class ExecuteProcessor : IProcessor
    {
        private enum ProcessorMode { Direct, Switch }
        private struct ExecuteTarget
        {
            internal ScriptModel Model;
            internal ScriptScope Scope;
            internal IProcessor Processor;
            internal ExecuteTarget(ScriptModel model, ScriptScope scope, IProcessor processor)
            {
                Model = model;
                Scope = scope;
                Processor = processor;
            }
        }

        private IProcessor _next;
        private readonly ScriptScope _scope;
        private readonly ExecuteStatement _statement;
        private readonly ExecuteKind _kind = ExecuteKind.Default;
        private readonly ProcessorMode _mode = ProcessorMode.Direct;
        private readonly List<DataObject> _tasks;
        private const string DIRECT_CALL_TARGET = "__DIRECT__";
        private const string DEFAULT_SWITCH_TARGET = "__DEFAULT__";
        private readonly Dictionary<string, ExecuteTarget> _targets = new();
        public ExecuteProcessor(in ScriptScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));

            if (_scope.Owner is not ExecuteStatement statement)
            {
                throw new ArgumentException(nameof(ExecuteStatement));
            }

            _statement = statement;
            _kind = _statement.Kind;
            _mode = _statement.Uri.Contains('@') ? ProcessorMode.Switch : ProcessorMode.Direct;

            if (_kind == ExecuteKind.Task || _kind == ExecuteKind.Work)
            {
                _tasks = GetTaskArrayReference();
            }
        }
        public void Dispose()
        {
            foreach (ExecuteTarget target in _targets.Values)
            {
                target.Processor?.Dispose();
            }

            _targets.Clear();
            _next?.Dispose();
        }
        public void Synchronize()
        {
            if (_kind == ExecuteKind.Sync)
            {
                Execute();
            }

            foreach (ExecuteTarget target in _targets.Values)
            {
                target.Processor?.Synchronize();
            }

            _next?.Synchronize();
        }
        public void LinkTo(in IProcessor next) { _next = next; }
        public void Process()
        {
            if (_kind != ExecuteKind.Sync)
            {
                Execute(); // Default, Task, Work
            }
            
            _next?.Process();
        }
        private void Execute()
        {
            try
            {
                ExecuteTarget target = GetExecuteTarget(); //THINK: avoid recursive script invocation !?

                InitializeTargetScopeVariables(target.Scope); //NOTE: copy input variable values from caller's scope

                IProcessor processor = target.Processor;

                if (_kind == ExecuteKind.Task || _kind == ExecuteKind.Work)
                {
                    processor = StreamFactory.CreateStream(target.Scope);
                }

                if (processor is null)
                {
                    throw new InvalidOperationException($"[EXECUTE] {_statement.Uri} Failed to compile script");
                }
                
                if (_kind == ExecuteKind.Task)
                {
                    StoreTaskToWait(Task.Factory.StartNew(processor.Process)); // Default .NET thread pool
                }
                else if (_kind == ExecuteKind.Work)
                {
                    StoreTaskToWait(Task.Factory.StartNew(processor.Process, TaskCreationOptions.LongRunning));
                }
                else // Default and Sync
                {
                    processor.Process();
                }
            }
            catch (ReturnException _return)
            {
                if (_kind == ExecuteKind.Task || _kind == ExecuteKind.Work) // ???
                {
                    return; // IGNORE
                }

                object value = _return.Value;

                if (_statement.Return is null)
                {
                    throw new InvalidOperationException($"[EXECUTE] {_statement.Uri} Return variable is not defined");
                }

                if (!_scope.TrySetValue(_statement.Return.Identifier, value)) // set return value to the caller's scope variable
                {
                    throw new InvalidOperationException($"[EXECUTE] {_statement.Uri} Error returning into {_statement.Return.Identifier}");
                }

                ConfigureReturnObjectSchema();
            }
            catch (Exception error) // Unexpected error
            {
                FileLogger.Default.Write(error); throw;
            }
        }
        
        private void StoreTaskToWait(in Task task)
        {
            if (_tasks is null) { return; }

            DataObject item = new(8);
            item.SetValue("Id", task.Id);
            item.SetValue("Task", task);
            item.SetValue("Result", null);
            item.SetValue("Status", task.Status.ToString());
            item.SetValue("IsFaulted", task.IsFaulted);
            item.SetValue("IsCanceled", task.IsCanceled);
            item.SetValue("IsCompleted", task.IsCompleted);
            item.SetValue("IsSucceeded", task.IsCompletedSuccessfully);
            _tasks.Add(item);
        }
        private List<DataObject> GetTaskArrayReference()
        {
            if (_statement.Return is null) { return null; }

            string identifier = _statement.Return.Identifier;

            if (!_scope.TryGetDeclaration(in identifier, out _, out DeclareStatement target))
            {
                throw new InvalidOperationException($"[EXECUTE] {_statement.Uri} Declaration of {identifier} is not found");
            }

            if (target.Type.Token != TokenType.Array)
            {
                throw new InvalidOperationException($"[EXECUTE] {_statement.Uri} Variable {identifier} must be of type [array]");
            }

            if (!_scope.TryGetValue(in identifier, out object value))
            {
                throw new InvalidOperationException($"[EXECUTE] task array variable {identifier} is not found");
            }

            if (value is not List<DataObject> tasks)
            {
                tasks = new List<DataObject>();

                if (!_scope.TrySetValue(in identifier, tasks))
                {
                    throw new InvalidOperationException($"[EXECUTE] {_statement.Uri} Error setting value of {identifier}");
                }
            }

            return tasks;
        }

        private string GetExecuteScriptPath()
        {
            string[] templates = _scope.GetUriTemplates(_statement.Uri);

            Uri uri = templates.Length == 0
                ? new(_statement.Uri)
                : new(_scope.ReplaceUriTemplates(_statement.Uri, in templates));

            return uri.LocalPath[2..];
        }
        private string GetDefaultScriptPath()
        {
            string[] templates = _scope.GetUriTemplates(_statement.Default);

            Uri uri = templates.Length == 0
                ? new(_statement.Default)
                : new(_scope.ReplaceUriTemplates(_statement.Default, in templates));

            return uri.LocalPath[2..];
        }
        private string GetScriptSourceCode(in string scriptPath)
        {
            string file = Path.Combine(AppContext.BaseDirectory, scriptPath);

            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                file = file.Replace('\\', '/'); // Linux compliant path
            }

            if (!File.Exists(file))
            {
                return string.Empty;
            }

            string script;

            using (StreamReader reader = new(file, Encoding.UTF8))
            {
                script = reader.ReadToEnd();
            }

            return script;
        }
        private object CopySourceScopeVariableValue(in object value)
        {
            object copy;

            if (value is DataObject record)
            {
                copy = record.Copy();
            }
            else if (value is List<DataObject> source)
            {
                List<DataObject> array = new(source.Count);

                for (int i = 0; i < source.Count; i++)
                {
                    array.Add(source[i].Copy());
                }

                copy = array;
            }
            else
            {
                copy = value; //THINK: !?
            }

            return copy;
        }
        private void InitializeTargetScopeVariables(in ScriptScope target)
        {
            if (_statement.Parameters is null) { return; }

            foreach (ColumnExpression parameter in _statement.Parameters)
            {
                string identifier = "@" + parameter.Alias;

                if (!StreamFactory.TryEvaluate(in _scope, parameter.Expression, out object value))
                {
                    throw new InvalidOperationException($"[EXECUTE] {_statement.Uri} Error reading parameter {identifier}");
                }

                object result;

                if (_kind == ExecuteKind.Task || _kind == ExecuteKind.Work)
                {
                    result = CopySourceScopeVariableValue(in value);
                }
                else
                {
                    result = value;
                }

                if (!target.TrySetValue(in identifier, in result))
                {
                    throw new InvalidOperationException($"[EXECUTE] {_statement.Uri} Error setting parameter {identifier}");
                }
            }
        }

        private ExecuteTarget GetExecuteTarget()
        {
            if (_kind == ExecuteKind.Task ||
                _kind == ExecuteKind.Work)
            {
                return GetAsyncTarget();
            }
            else // Default or Sync
            {
                return GetSyncTarget();
            }
        }
        private ExecuteTarget GetSyncTarget()
        {
            if (_mode == ProcessorMode.Direct)
            {
                return GetSyncDirectTarget();
            }
            else
            {
                return GetSyncSwitchTarget();
            }
        }
        private ExecuteTarget GetSyncDirectTarget()
        {
            if (_targets.TryGetValue(DIRECT_CALL_TARGET, out ExecuteTarget target))
            {
                return target;
            }

            string scriptPath = GetExecuteScriptPath();
            string sourceCode = GetScriptSourceCode(in scriptPath);

            if (string.IsNullOrEmpty(sourceCode))
            {
                throw new InvalidOperationException($"Script not found: {scriptPath}");
            }

            if (!new ScriptParser().TryParse(in sourceCode, out ScriptModel model, out string error))
            {
                throw new InvalidOperationException(error);
            }

            ScriptScope scope = _scope.Create(in model);
            StreamFactory.InitializeVariables(in scope);
            IProcessor script = StreamFactory.CreateStream(in scope);

            ExecuteTarget compiled = new(model, scope, script);

            _targets.Add(DIRECT_CALL_TARGET, compiled);

            return compiled;
        }
        private ExecuteTarget GetSyncSwitchTarget()
        {
            string scriptPath = GetExecuteScriptPath();

            if (_targets.TryGetValue(scriptPath, out ExecuteTarget target))
            {
                return target;
            }

            bool useDefaultSwitch = false;

            string sourceCode = GetScriptSourceCode(in scriptPath);

            if (string.IsNullOrEmpty(sourceCode))
            {
                if (_targets.TryGetValue(DEFAULT_SWITCH_TARGET, out ExecuteTarget _default))
                {
                    return _default;
                }
                
                if (!string.IsNullOrEmpty(_statement.Default))
                {
                    scriptPath = GetDefaultScriptPath();
                    sourceCode = GetScriptSourceCode(in scriptPath);
                    useDefaultSwitch = true;
                }
            }

            if (string.IsNullOrEmpty(sourceCode))
            {
                throw new InvalidOperationException($"Script not found: {scriptPath}");
            }

            if (!new ScriptParser().TryParse(in sourceCode, out ScriptModel model, out string error))
            {
                throw new InvalidOperationException(error);
            }

            ScriptScope scope = _scope.Create(in model);
            StreamFactory.InitializeVariables(in scope);
            IProcessor script = StreamFactory.CreateStream(in scope);

            ExecuteTarget compiled = new(model, scope, script);

            if (useDefaultSwitch)
            {
                _targets.Add(DEFAULT_SWITCH_TARGET, compiled);
            }
            else
            {
                _targets.Add(scriptPath, compiled);
            }

            return compiled;
        }
        private ExecuteTarget GetAsyncTarget()
        {
            if (_mode == ProcessorMode.Direct)
            {
                return GetAsyncDirectTarget();
            }
            else
            {
                return GetAsyncSwitchTarget();
            }
        }
        private ExecuteTarget GetAsyncDirectTarget()
        {
            ScriptModel script = null;

            if (_targets.TryGetValue(DIRECT_CALL_TARGET, out ExecuteTarget target))
            {
                script = target.Model;
            }
            else
            {
                string scriptPath = GetExecuteScriptPath();
                string sourceCode = GetScriptSourceCode(in scriptPath);

                if (string.IsNullOrEmpty(sourceCode))
                {
                    throw new InvalidOperationException($"Script not found: {scriptPath}");
                }

                if (!new ScriptParser().TryParse(in sourceCode, out script, out string error))
                {
                    throw new InvalidOperationException(error);
                }

                ExecuteTarget cache = new(script, null, null);

                _targets.Add(DIRECT_CALL_TARGET, cache);
            }

            if (script is null)
            {
                throw new InvalidOperationException($"[EXECUTE] {_statement.Uri} failed to create script");
            }

            ScriptScope scope = _scope.Create(in script);
            StreamFactory.InitializeVariables(in scope);
            return new ExecuteTarget(script, scope, null);
        }
        private ExecuteTarget GetAsyncSwitchTarget()
        {
            ScriptModel script = null;

            string scriptPath = GetExecuteScriptPath();

            if (_targets.TryGetValue(scriptPath, out ExecuteTarget target))
            {
                script = target.Model;
            }

            if (script is null) // switch cache missed
            {
                string sourceCode = GetScriptSourceCode(in scriptPath);

                bool useDefaultSwitch = false;

                if (string.IsNullOrEmpty(sourceCode)) // switch script is not found, try default path
                {
                    if (string.IsNullOrEmpty(_statement.Default))
                    {
                        throw new InvalidOperationException($"Script not found: {scriptPath}");
                    }

                    useDefaultSwitch = true;

                    if (_targets.TryGetValue(DEFAULT_SWITCH_TARGET, out ExecuteTarget _default))
                    {
                        script = _default.Model;
                    }
                    else
                    {
                        scriptPath = GetDefaultScriptPath();
                        sourceCode = GetScriptSourceCode(in scriptPath);

                        if (string.IsNullOrEmpty(sourceCode))
                        {
                            throw new InvalidOperationException($"Script not found: {scriptPath}");
                        }
                    }
                }

                if (script is null) // default cache missed
                {
                    if (!new ScriptParser().TryParse(in sourceCode, out script, out string error))
                    {
                        throw new InvalidOperationException(error);
                    }

                    ExecuteTarget cache = new(script, null, null);

                    if (useDefaultSwitch)
                    {
                        _targets.Add(DEFAULT_SWITCH_TARGET, cache);
                    }
                    else
                    {
                        _targets.Add(scriptPath, cache);
                    }
                }
            }

            ScriptScope scope = _scope.Create(in script);
            StreamFactory.InitializeVariables(in scope);
            return new ExecuteTarget(script, scope, null);
        }

        private void ConfigureReturnObjectSchema()
        {
            if (_statement.Return is null) { return; }

            string identifier = _statement.Return.Identifier;

            if (!_scope.TryGetDeclaration(in identifier, out _, out DeclareStatement target))
            {
                throw new InvalidOperationException($"Declaration of {identifier} is not found");
            }

            //if (_scope.TryGetDeclaration(in identifier, out _, out DeclareStatement source))
            //{
            //    //TODO: get return value schema from return statement
            //    target.Type.Binding = source.Type.Binding;
            //}
        }
    }
}