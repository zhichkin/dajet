using DaJet.Data;
using DaJet.Data.Client;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using System.Reflection.Metadata;

namespace DaJet.Stream
{
    internal abstract class ProcessorBase : IProcessor
    {
        protected IProcessor _next;
        protected readonly Pipeline _pipeline;
        protected readonly SqlStatement _statement;
        protected string _arrayName;
        protected string _objectName;
        protected List<string> _memberNames = new();
        protected List<string> _parameterNames = new();
        protected StatementType _mode = StatementType.Processor;
        internal ProcessorBase(in Pipeline pipeline, in SqlStatement statement)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _statement = statement ?? throw new ArgumentNullException(nameof(statement));

            Configure();
        }
        public void LinkTo(in IProcessor next)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
        }
        public abstract void Process();
        public abstract void Synchronize();
        private void Configure()
        {
            List<VariableReference> variables = new VariableReferenceExtractor().Extract(_statement.Node);

            foreach (VariableReference variable in variables)
            {
                if (variable.Binding is TypeIdentifier type)
                {
                    if (type.Token == TokenType.Array)
                    {
                        _mode = StatementType.Buffering;
                        _arrayName = variable.Identifier;
                    }
                    else if (type.Token == TokenType.Object)
                    {
                        _mode = StatementType.Streaming;
                        _objectName = variable.Identifier;
                    }

                    if (_mode != StatementType.Processor)
                    {
                        if (!_pipeline.Parameters.ContainsKey(variable.Identifier))
                        {
                            _pipeline.Parameters.Add(variable.Identifier, null);
                        }
                    }
                }
                else
                {
                    _parameterNames.Add(variable.Identifier); //TODO: deduplicate !!!
                }
            }

            List<MemberAccessExpression> expressions = new MemberAccessExtractor().Extract(_statement.Node);

            foreach (MemberAccessExpression expression in expressions)
            {
                string[] identifier = expression.Identifier.Split('.');
                string target = identifier[0];
                string member = identifier[1];
                string memberName = $"{target}_{member}";

                if (!_pipeline.Parameters.ContainsKey(memberName))
                {
                    _pipeline.Parameters.Add(memberName, null);
                }

                _memberNames.Add(memberName); //TODO: deduplicate !!!
            }
        }
        protected void ConfigureParameters(in OneDbCommand command)
        {
            Dictionary<string, object> parameters = new();

            foreach (string memberName in _memberNames)
            {
                string[] identifier = memberName.Split('_');
                string target = identifier[0];
                string member = identifier[1];

                if (_pipeline.Parameters.TryGetValue(target, out object value))
                {
                    if (value is DataObject record)
                    {
                        if (record.TryGetValue(member, out value))
                        {
                            parameters.Add(memberName, value);
                        }
                    }
                }
            }

            _pipeline.Context.ConfigureDbParameters(in parameters);

            //TODO: fix parameters naming ?

            foreach (var parameter in parameters)
            {
                _pipeline.Parameters[parameter.Key] = parameter.Value;
                command.Parameters.Add(parameter.Key[1..], parameter.Value);
            }

            foreach (string parameterName in _parameterNames)
            {
                if (_pipeline.Parameters.TryGetValue(parameterName[1..], out object value))
                {
                    command.Parameters.Add(parameterName[1..], value);
                }
            }
        }
    }
}