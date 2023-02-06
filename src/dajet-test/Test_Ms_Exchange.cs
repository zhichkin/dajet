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
    [TestClass] public class Test_Ms_Exchange
    {
        private const string IB_KEY = "dajet-metadata-ms";
        private readonly InfoBase _infoBase;
        private readonly MetadataCache _cache;
        private readonly MetadataService _service = new();
        private const string MS_CONNECTION_STRING = "Data Source=ZHICHKIN;Initial Catalog=dajet-metadata-ms;Integrated Security=True;Encrypt=False;";
        public Test_Ms_Exchange()
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
        private bool TryGenerateScript(string filePath, out GeneratorResult result)
        {
            result = null!;
            ScriptModel model = null!;
            string error = string.Empty;

            using (StreamReader reader = new(filePath, Encoding.UTF8))
            {
                string script = reader.ReadToEnd();

                using (ScriptParser parser = new())
                {
                    if (!parser.TryParse(in script, out model, out error))
                    {
                        Console.WriteLine(error); return false;
                    }
                }
            }

            if (!(new ScopeBuilder()).TryBuild(in model, out ScriptScope scope, out error))
            {
                Console.WriteLine(error); return false;
            }

            if (!(new MetadataBinder()).TryBind(in scope, in _cache, out error))
            {
                Console.WriteLine(error); return false;
            }

            if (!(new ScriptTransformer()).TryTransform(model, out error))
            {
                Console.WriteLine(error); return false;
            }

            return new MsSqlGenerator().TryGenerate(in model, out result);
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
        private void ShowEntityMap(EntityMap map)
        {
            foreach (PropertyMap property in map.Properties)
            {
                Console.WriteLine($"{property.Name} [{property.Type}] {property.TypeCode}");

                foreach (var item in property.Columns)
                {
                    ColumnMap column = item.Value;

                    Console.WriteLine($" - {column.Name} [{column.Purpose}] {column.Alias}");
                }
            }
        }
        private void ShowEntity(Dictionary<string, object> entity)
        {
            foreach (var property in entity)
            {
                Console.WriteLine($"{property.Key} = {property.Value}");
            }
            Console.WriteLine();
        }
        private void ShowPropertyValues(object entity)
        {
            Type type = entity.GetType();

            foreach (PropertyInfo property in type.GetProperties())
            {
                object? value = property.GetValue(entity, null);
                Console.WriteLine($"{property.Name} = {value}");
            }
        }
        [TestMethod] public void Script_Catalog()
        {
            foreach (string filePath in Directory.GetFiles("C:\\temp\\scripting-test\\exchange"))
            {
                if (!TryGenerateScript(filePath, out GeneratorResult result))
                {
                    Console.WriteLine(result.Error); continue;
                }
                else
                {
                    Console.WriteLine();
                    Console.WriteLine(filePath);
                    Console.WriteLine();
                    Console.WriteLine(result.Script);

                    ShowEntityMap(result.Mapper);
                }
            }
        }
        [TestMethod] public void Execute_Catalog()
        {
            ScriptExecutor executor = new(_cache);

            string script;

            foreach (string filePath in Directory.GetFiles("C:\\temp\\scripting-test\\exchange"))
            {
                using (StreamReader reader = new(filePath, Encoding.UTF8))
                {
                    script = reader.ReadToEnd();
                }

                Console.WriteLine();
                Console.WriteLine(filePath);

                try
                {
                    foreach (var entity in executor.ExecuteReader(script))
                    {
                        Console.WriteLine();
                        ShowEntity(entity);
                    }
                }
                catch (Exception exception)
                {
                    Console.WriteLine(ExceptionHelper.GetErrorMessage(exception));
                }
            }
        }
        [TestMethod] public void Execute_Catalog_TablePart()
        {
            ScriptExecutor executor = new(_cache);

            string script;

            foreach (string filePath in Directory.GetFiles("C:\\temp\\scripting-test\\table-parts"))
            {
                using (StreamReader reader = new(filePath, Encoding.UTF8))
                {
                    script = reader.ReadToEnd();
                }

                Console.WriteLine();
                Console.WriteLine(filePath);

                try
                {
                    foreach (var entity in executor.ExecuteReader(script))
                    {
                        Console.WriteLine();
                        ShowEntity(entity);
                    }
                }
                catch (Exception exception)
                {
                    Console.WriteLine(ExceptionHelper.GetErrorMessage(exception));
                }
            }
        }
    }
}