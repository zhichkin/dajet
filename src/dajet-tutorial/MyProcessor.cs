using DaJet.Data;
using DaJet.Runtime;
using DaJet.Scripting.Model;
using DaJet.Runtime;

namespace DaJet.Tutorial
{
    public sealed class MyProcessor : UserDefinedProcessor
    {
        public MyProcessor(in StreamScope scope) : base(scope) { }
        public override void Process() { Execute(); _next?.Process(); }
        private void Execute()
        {
            FileLogger.Default.Write("[MyProcessor] START");

            FileLogger.Default.Write("[MyProcessor] VARIABLES:");

            foreach (VariableReference variable in _statement.Variables)
            {
                if (_scope.TryGetValue(variable.Identifier, out object value))
                {
                    FileLogger.Default.Write($"{variable.Identifier} = {(value is null ? String.Empty : value.ToString())}");
                }
            }

            FileLogger.Default.Write("[MyProcessor] OPTIONS:");

            foreach (var item in _options)
            {
                object value = item.Value;

                FileLogger.Default.Write($"{item.Key} = {(value is null ? String.Empty : value.ToString())}");
            }

            if (_statement.Return is not null)
            {
                FileLogger.Default.Write("[MyProcessor] RETURN = " + _statement.Return.Identifier);

                ConfigureReturnValue("200", "RETURN VALUE");
            }

            FileLogger.Default.Write("[MyProcessor] END");
        }
        private void ConfigureReturnValue(in string code, in string result)
        {
            if (_statement.Return is null) { return; }

            if (!_scope.TryGetValue(_statement.Return.Identifier, out object value))
            {
                throw new InvalidOperationException($"Return variable {_statement.Return.Identifier} is not found");
            }

            if (value is null)
            {
                value = new DataObject(2);

                if (!_scope.TrySetValue(_statement.Return.Identifier, value))
                {
                    throw new InvalidOperationException($"Failed to set return variable {_statement.Return.Identifier}");
                }
            }

            if (value is DataObject @object)
            {
                @object.SetValue("Code", code);
                @object.SetValue("Value", result is null ? string.Empty : result);
            }
        }
    }
}