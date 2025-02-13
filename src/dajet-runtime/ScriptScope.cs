using DaJet.Data;
using DaJet.Metadata;
using DaJet.Scripting;
using DaJet.Scripting.Model;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace DaJet.Runtime
{
    public sealed class ScriptScope : IScriptRuntime
    {
        private static readonly object _metadata_providers_lock = new();
        public ScriptScope(SyntaxNode owner)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }
        public ScriptScope(SyntaxNode owner, ScriptScope parent) : this(owner)
        {
            Parent = parent ?? throw new ArgumentNullException(nameof(parent));
        }
        public SyntaxNode Owner { get; set; }
        public ScriptScope Parent { get; set; }
        public List<ScriptScope> Children { get; } = new();
        public string ErrorMessage { get; set; } = string.Empty;
        public override string ToString() { return $"Owner: {Owner}"; }
        public ScriptScope GetRoot()
        {
            ScriptScope root = this;

            while (root.Parent is not null)
            {
                root = root.Parent;
            }

            return root;
        }
        public ScriptScope GetParent<TOwner>() where TOwner : SyntaxNode
        {
            Type type = typeof(TOwner);

            ScriptScope scope = this;
            SyntaxNode owner = Owner;

            while (scope is not null)
            {
                if (owner is not null && owner.GetType() == type)
                {
                    return scope;
                }

                scope = scope.Parent;
                owner = scope?.Owner;
            }

            return null;
        }
        public ScriptScope Open(in SyntaxNode owner)
        {
            ScriptScope scope = new(owner, this);

            Children.Add(scope);

            return scope;
        }

        public ScriptScope Close() { return Parent; }
        public ScriptScope Clone()
        {
            ScriptScope clone = new(Owner, this);

            foreach (ScriptScope child in Children)
            {
                clone.Children.Add(child.Clone(in clone));
            }

            return clone;
        }
        private ScriptScope Clone(in ScriptScope parent)
        {
            ScriptScope clone = new(Owner, parent);

            foreach (ScriptScope child in Children)
            {
                clone.Children.Add(child.Clone(in clone));
            }

            return clone;
        }
        public ScriptScope Create(in StatementBlock statements)
        {
            ScriptScope scope = new(statements, this);

            Children.Add(scope);

            for (int i = 0; i < statements.Statements.Count; i++)
            {
                SyntaxNode statement = statements.Statements[i];

                if (statement is CommentStatement) { continue; }

                if (statement is DeclareStatement declare)
                {
                    scope.Variables.Add(declare.Name, null);
                    
                    scope.Declarations.Add(declare);
                }
                else
                {
                    ScriptScope child = new(statement, scope);

                    scope.Children.Add(child);
                }
            }

            return scope;
        }
        public static ScriptScope Create(in ScriptModel script, in ScriptScope parent)
        {
            ScriptScope scope = parent is null ? new(script) : new(script, parent);

            parent?.Children.Add(scope);

            for (int i = 0; i < script.Statements.Count; i++)
            {
                SyntaxNode statement = script.Statements[i];

                if (statement is CommentStatement) { continue; }

                if (statement is DeclareStatement declare)
                {
                    scope.Variables.Add(declare.Name, null);
                    
                    scope.Declarations.Add(declare);
                }
                else
                {
                    ScriptScope child = new(statement, scope);
                    
                    scope.Children.Add(child);
                }
            }

            return scope;
        }
        public ScriptScope Create(in ScriptModel script) { return Create(in script, this); }
        public List<DeclareStatement> Declarations { get; } = new(); // order is important for binding
        public Dictionary<string, object> Variables { get; } = new(); // scope variables and their values
        public Dictionary<SyntaxNode, SqlStatement> Transpilations { get; } = new(); // script transpilations cache
        public ConcurrentDictionary<string, IMetadataProvider> MetadataProviders { get; } = new(); // metadata providers cache
        public bool TrySetValue(in string name, in object value)
        {
            ScriptScope scope = this;

            while (scope is not null)
            {
                if (scope.Variables.ContainsKey(name))
                {
                    scope.Variables[name] = value; return true;
                }

                scope = scope.Parent;
            }

            return false;
        }
        /// <summary>Получает значение переменной контекста выполнения или её членов
        /// <br/>Параметр <b>expression</b> это идентификатор <see cref="VariableReference"/> или <see cref="MemberAccessExpression"/>
        /// <br/>Например: @variable или @variable.member
        /// </summary>
        /// <param name="expression">Выражение доступа к членам переменной<br/>@variable или @variable.member</param>
        /// <param name="value">Результат вычисления выражения</param>
        /// <returns>Выражение удалось вычислить успешно или нет</returns>
        public bool TryGetValue(in string expression, out object value)
        {
            value = null;

            List<string> members = ParserHelper.GetAccessMembers(in expression);

            for (int current = 0; current < members.Count; current++)
            {
                if (current == 0) // root member access variable
                {
                    if (!TryGetScopeValue(members[0], out value))
                    {
                        return false;
                    }
                }
                else
                {
                    if (!TryGetMemberValue(members[current], ref value))
                    {
                        value = null;
                        return false;
                    }
                }
            }

            return true;
        }
        private bool TryGetScopeValue(in string name, out object value)
        {
            value = null;

            ScriptScope scope = this;

            while (scope is not null)
            {
                if (scope.Variables.TryGetValue(name, out value))
                {
                    return true;
                }

                scope = scope.Parent;
            }

            return false;
        }
        private bool TryGetMemberValue(in string member, ref object target)
        {
            if (target is DataObject data)
            {
                return data.TryGetValue(member, out target);
            }
            else if (target is string json)
            {
                target = ScriptRuntimeFunctions.FromJson(this, in json); //TODO: call function via IScriptRuntime !?

                return TryGetMemberValue(in member, ref target);
            }
            else if (target is List<DataObject> array)
            {
                if (member.StartsWith('[') && member.EndsWith(']'))
                {
                    string selector = member.TrimStart('[').TrimEnd(']');

                    if (int.TryParse(selector, out int index))
                    {
                        if (array.Count > index)
                        {
                            target = array[index];
                            return true;
                        }
                        else
                        {
                            target = null;
                            return false;
                        }
                    }
                    else // name = { true | 12 | 1.2 | 'abc' | @variable }
                    {
                        return TryGetValueByFilter(in array, in selector, out target);
                    }
                }
                else
                {
                    throw new FormatException(nameof(member));
                }
            }
            
            target = null;
            return false;
        }
        private bool TryGetValueByFilter(in List<DataObject> array, in string filter, out object value)
        {
            string[] filters = filter.Split('=');

            string name = filters[0];
            string test = filters[1];

            if (test.StartsWith('@'))
            {
                if (TryGetValue(test, out value))
                {
                    test = value?.ToString();
                }
                else
                {
                    value = null;
                    return false;
                }
            }
            else if (test.StartsWith('\''))
            {
                test = test.TrimStart('\'').TrimEnd('\'');
            }

            foreach (DataObject item in array)
            {
                if (item.GetValue(name).ToString() == test)
                {
                    value = item; return true;
                }
            }

            //bool equal = ((value != null) && value.GetType().IsValueType)
            //         ? value.Equals(_properties[property])
            //         : (value == _properties[property]);

            value = null;
            return false;
        }
        public bool TryGetDeclaration(in string name, out bool local, out DeclareStatement declare)
        {
            local = true;
            declare = null;

            ScriptScope scope = this;

            while (scope is not null)
            {
                for (int i = 0; i < scope.Declarations.Count; i++)
                {
                    declare = scope.Declarations[i];

                    if (declare.Name == name)
                    {
                        return true;
                    }
                }

                scope = scope.Parent;

                local = false;
            }

            return false;
        }
        public bool TryGetTranspilation(in SyntaxNode owner, out SqlStatement statement)
        {
            statement = null;

            ScriptScope scope = this;

            while (scope is not null)
            {
                if (scope.Transpilations.TryGetValue(owner, out statement))
                {
                    return true;
                }

                scope = scope.Parent;
            }

            return false;
        }
        public bool TryGetMetadataProvider(out IMetadataProvider provider, out string error)
        {
            error = null;

            Uri uri = GetDatabaseUri(); //NOTE: constructs uri dynamically

            ScriptScope root = GetRoot(); //NOTE: ScriptModel is the root

            string connectionString = uri.ToString();

            if (root.MetadataProviders.TryGetValue(connectionString, out provider))
            {
                return true;
            }

            try
            {
                lock (_metadata_providers_lock) //TODO: lock by connection string value
                {
                    if (root.MetadataProviders.TryGetValue(connectionString, out provider))
                    {
                        return true;
                    }

                    provider = MetadataService.CreateOneDbMetadataProvider(in uri);

                    _ = root.MetadataProviders.TryAdd(connectionString, provider);
                }
            }
            catch (Exception exception)
            {
                error = ExceptionHelper.GetErrorMessage(exception);
            }

            return error is null;
        }

        private static readonly Regex _uri_template = new("{(.*?)}", RegexOptions.CultureInvariant);
        public string[] GetUriTemplates(in string uri)
        {
            MatchCollection matches = _uri_template.Matches(uri);

            if (matches.Count == 0)
            {
                return Array.Empty<string>();
            }

            string[] templates = new string[matches.Count];

            for (int i = 0; i < matches.Count; i++)
            {
                templates[i] = matches[i].Value;
            }

            return templates;
        }
        public string ReplaceUriTemplates(in string uri, in string[] templates)
        {
            string result = uri;

            for (int i = 0; i < templates.Length; i++)
            {
                string variable = templates[i].TrimStart('{').TrimEnd('}');

                if (TryGetValue(in variable, out object value))
                {
                    result = result.Replace(templates[i], value.ToString());
                }
                else
                {
                    result = result.Replace(templates[i], string.Empty);
                }
            }

            return result;
        }
        public Uri GetUri(in string uri)
        {
            string[] templates = GetUriTemplates(in uri);

            if (templates.Length == 0)
            {
                return new Uri(uri);
            }

            string result = ReplaceUriTemplates(in uri, in templates);

            return new Uri(result);
        }
        public Uri GetDatabaseUri()
        {
            ScriptScope parent = GetParent<UseStatement>() ?? throw new InvalidOperationException("Parent UseStatement is not found");
            
            if (parent.Owner is UseStatement use)
            {
                return parent.GetUri(use.Uri);
            }

            throw new InvalidOperationException("Owner UseStatement is not found");
        }
    }
}