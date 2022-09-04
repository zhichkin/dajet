using DaJet.Data;
using DaJet.Metadata;
using DaJet.Metadata.Model;
using DaJet.Scripting.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections;
using System.Reflection;
using System.Text;

namespace DaJet.Scripting.Test
{
    [TestClass] public class Test_Binder
    {
        private const string IB_KEY = "dajet-metadata-ms";
        private readonly InfoBase _infoBase;
        private readonly MetadataCache _cache;
        private readonly MetadataService _service = new();
        private const string MS_CONNECTION_STRING = "Data Source=ZHICHKIN;Initial Catalog=dajet-metadata-ms;Integrated Security=True;Encrypt=False;";
        private string filePath = "C:\\temp\\scripting-test\\script04.txt";
        public Test_Binder()
        {
            _service.Add(new InfoBaseOptions()
            {
                Key = IB_KEY,
                ConnectionString = MS_CONNECTION_STRING,
                DatabaseProvider = DatabaseProvider.SqlServer
            });

            if (!_service.TryGetInfoBase(IB_KEY, out _infoBase, out string error))
            {
                throw new InvalidOperationException($"Failed to open info base: {error}");
            }

            if (!_service.TryGetMetadataCache(IB_KEY, out _cache, out error))
            {
                throw new InvalidOperationException($"Failed to get metadata cache: {error}");
            }
        }

        private ScriptModel _syntaxTree;
        private void CreateSyntaxTree()
        {
            using (StreamReader reader = new(filePath, Encoding.UTF8))
            {
                string script = reader.ReadToEnd();

                using (ScriptParser parser = new())
                {
                    if (!parser.TryParse(in script, out _syntaxTree, out string error))
                    {
                        Console.WriteLine(error);
                    }
                }
            }
        }
        private void ShowSyntaxNode(SyntaxNode node, int level)
        {
            if (node == null)
            {
                return;
            }

            string dbname = string.Empty;
            string offset = "-".PadLeft(level * 2);

            if (node is Identifier identifier)
            {
                dbname = "[" + identifier.Tag?.ToString() + "] " + identifier.Value
                    + (string.IsNullOrEmpty(identifier.Alias)
                    ? string.Empty
                    : " AS " + identifier.Alias);
            }

            Console.WriteLine($"{offset} {node} {dbname}");

            foreach (PropertyInfo property in node.GetType().GetProperties())
            {
                if (property.Name == "Nodes")
                {
                    continue;
                }

                Type propertyType = property.PropertyType;

                bool isList = (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(List<>));

                if (isList)
                {
                    propertyType = propertyType.GetGenericArguments()[0];
                }

                if (propertyType != typeof(SyntaxNode) && !propertyType.IsSubclassOf(typeof(SyntaxNode)))
                {
                    continue;
                }

                object value = property.GetValue(node);

                if (value == null)
                {
                    continue;
                }

                if (isList)
                {
                    IList list = (IList)value;
                    for (int i = 0; i < list.Count; i++)
                    {
                        ShowSyntaxNode(list[i] as SyntaxNode, level + 1);
                    }
                }
                else
                {
                    ShowSyntaxNode(value as SyntaxNode, level + 1);
                }
            }
        }
        [TestMethod] public void Bind_Metadata()
        {
            CreateSyntaxTree();

            if (_syntaxTree == null)
            {
                return;
            }

            ScopeBuilder builder = new();

            if (!builder.TryBuild(in _syntaxTree, out ScriptScope scope, out string error))
            {
                Console.WriteLine(error);
                return;
            }

            MetadataBinder binder = new();

            if (!binder.TryBind(in scope, in _cache, out error))
            {
                Console.WriteLine(error);
                return;
            }

            ShowSyntaxNode(_syntaxTree, 0);
        }
        [TestMethod] public void Bind_Metadata_Cte()
        {
            filePath = "C:\\temp\\scripting-test\\cte\\04-script.txt";

            CreateSyntaxTree();

            if (_syntaxTree == null)
            {
                return;
            }

            ScopeBuilder builder = new();

            if (!builder.TryBuild(in _syntaxTree, out ScriptScope scope, out string error))
            {
                Console.WriteLine(error);
                return;
            }

            MetadataBinder binder = new();

            if (!binder.TryBind(in scope, in _cache, out error))
            {
                Console.WriteLine(error);
                return;
            }

            ShowSyntaxNode(_syntaxTree, 0);
        }
        [TestMethod] public void Bind_Metadata_Destructive_Read()
        {
            filePath = "C:\\temp\\scripting-test\\destructive-read\\00-script.txt";

            CreateSyntaxTree();

            if (_syntaxTree == null)
            {
                return;
            }

            ScopeBuilder builder = new();

            if (!builder.TryBuild(in _syntaxTree, out ScriptScope scope, out string error))
            {
                Console.WriteLine(error);
                return;
            }

            MetadataBinder binder = new();

            if (!binder.TryBind(in scope, in _cache, out error))
            {
                Console.WriteLine(error);
                return;
            }

            ShowSyntaxNode(_syntaxTree, 0);
        }
        [TestMethod] public void Transform_Script()
        {
            filePath = "C:\\temp\\scripting-test\\script05.txt";

            CreateSyntaxTree();

            if (_syntaxTree == null)
            {
                return;
            }

            ScopeBuilder builder = new();

            if (!builder.TryBuild(in _syntaxTree, out ScriptScope scope, out string error))
            {
                Console.WriteLine(error);
                return;
            }

            MetadataBinder binder = new();

            if (!binder.TryBind(in scope, in _cache, out error))
            {
                Console.WriteLine(error);
                return;
            }

            ScriptTransformer transformer = new();

            if (!transformer.TryTransform(_syntaxTree, out error))
            {
                Console.WriteLine(error);
                return;
            }

            ShowSyntaxNode(_syntaxTree, 0);
        }
        [TestMethod] public void Transform_Script_Variables()
        {
            filePath = "C:\\temp\\scripting-test\\script06.txt";

            CreateSyntaxTree(); // parser

            if (_syntaxTree == null)
            {
                return;
            }

            ScopeBuilder builder = new();

            if (!builder.TryBuild(in _syntaxTree, out ScriptScope scope, out string error))
            {
                Console.WriteLine(error);
                return;
            }

            MetadataBinder binder = new();
            
            if (!binder.TryBind(in scope, in _cache, out error)) // builder.Scope
            {
                Console.WriteLine(error);
                return;
            }

            ScriptTransformer transformer = new();

            if (!transformer.TryTransform(_syntaxTree, out error))
            {
                Console.WriteLine(error);
                return;
            }

            ShowSyntaxNode(_syntaxTree, 0);
        }
    }
}