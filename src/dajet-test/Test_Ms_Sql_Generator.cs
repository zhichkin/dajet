using DaJet.Data;
using DaJet.Data.Mapping;
using DaJet.Metadata;
using DaJet.Metadata.Model;
using DaJet.Scripting.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections;
using System.Reflection;
using System.Text;

namespace DaJet.Scripting.Test
{
    [TestClass] public class Test_Ms_Sql_Generator
    {
        private const string IB_KEY = "dajet-metadata-ms";
        private readonly InfoBase _infoBase;
        private readonly MetadataCache _cache;
        private readonly MetadataService _service = new();
        private const string MS_CONNECTION_STRING = "Data Source=ZHICHKIN;Initial Catalog=dajet-metadata-ms;Integrated Security=True;Encrypt=False;";
        private ScriptModel _model;
        private string filePath = "C:\\temp\\scripting-test\\script08.txt";
        public Test_Ms_Sql_Generator()
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
        private void CreateScriptModel()
        {
            using (StreamReader reader = new(filePath, Encoding.UTF8))
            {
                string script = reader.ReadToEnd();

                using (ScriptParser parser = new())
                {
                    if (!parser.TryParse(in script, out _model, out string error))
                    {
                        Console.WriteLine(error);
                    }
                }
            }
        }
        [TestMethod] public void Generate_Script()
        {
            filePath = "C:\\temp\\scripting-test\\destructive-read\\00-script.txt";

            CreateScriptModel();

            if (_model == null)
            {
                return;
            }

            ScopeBuilder builder = new();

            if (!builder.TryBuild(in _model, out ScriptScope scope, out string error))
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

            if (!transformer.TryTransform(_model, out error))
            {
                Console.WriteLine(error);
                return;
            }

            //ShowSyntaxNode(_model, 0);
            //Console.WriteLine();
            //Console.WriteLine("***");
            //Console.WriteLine();

            MsSqlGenerator generator = new();

            if (!generator.TryGenerate(_model, out GeneratorResult result))
            {
                Console.WriteLine(result.Error);
                return;
            }

            Console.WriteLine();
            Console.WriteLine(filePath);
            Console.WriteLine();
            Console.WriteLine(result.Script);
            
            ShowEntityMap(result.Mapper);
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
        [TestMethod] public void Execute_Script_Reader()
        {
            string script;
            filePath = "C:\\temp\\scripting-test\\script08.txt";

            Console.WriteLine();
            Console.WriteLine(filePath);
            Console.WriteLine();

            using (StreamReader reader = new(filePath, Encoding.UTF8))
            {
                script = reader.ReadToEnd();
            }

            ScriptExecutor executor = new(_cache);

            try
            {
                foreach (var entity in executor.ExecuteReader(script))
                {
                    ShowEntity(entity);
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(ExceptionHelper.GetErrorMessage(exception));
            }
        }
        [TestMethod] public void Execute_Script_Scalar()
        {
            string script;
            filePath = "C:\\temp\\scripting-test\\script09.txt";

            Console.WriteLine();
            Console.WriteLine(filePath);
            Console.WriteLine();

            using (StreamReader reader = new(filePath, Encoding.UTF8))
            {
                script = reader.ReadToEnd();
            }

            Guid uuid = new("cb0dcb72-1e6f-11ed-9cd5-408d5c93cc8e");

            ScriptExecutor executor = new(_cache);
            //executor.Parameters.Add("КодУзла", "N001");
            //executor.Parameters.Add("КодУзла", uuid);
            executor.Parameters.Add("КодУзла", new EntityRef(36, uuid));

            try
            {
                foreach (var entity in executor.ExecuteReader<NodeInfo>(script))
                {
                    Console.WriteLine(entity.ToString());
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(ExceptionHelper.GetErrorMessage(exception));
            }
        }
        [TestMethod] public void Simple_Script()
        {
            // Строка подключения к базе данных 1С
            string MS_CONNECTION_STRING = "Data Source=ZHICHKIN;Initial Catalog=dajet-metadata-ms;Integrated Security=True;Encrypt=False;";

            // Регистрируем настройки подключения к базе данных 1С по строковому ключу
            InfoBaseOptions options = new()
            {
                Key = "my_1c_infobase",
                ConnectionString = MS_CONNECTION_STRING,
                DatabaseProvider = DatabaseProvider.SqlServer
            };

            MetadataService metadata = new();
            metadata.Add(options);

            // Подключаемся к информационной базе 1С
            if (!metadata.TryGetMetadataCache(options.Key, out MetadataCache cache, out string error))
            {
                Console.WriteLine($"Ошибка открытия информационной базы: {error}");
                return;
            }

            ScriptExecutor executor = new(_cache);
            executor.Parameters.Add("КодТовара", "PRD 01");
            executor.Parameters.Add("ПометкаУдаления", false);

            string script = "ВЫБРАТЬ "
                + "Код             КАК Code, "
                + "Наименование    КАК Name, "
                + "Ссылка          КАК Reference, "
                + "ПометкаУдаления КАК IsMarkedForDeletion "
                + "ИЗ Справочник.Номенклатура "
                + "WHERE Код = @КодТовара "
                + "AND ПометкаУдаления = @ПометкаУдаления;";

            try
            {
                foreach (ProductInfo entity in executor.ExecuteReader<ProductInfo>(script))
                {
                    Console.WriteLine($"[{entity.Code}] {entity.Name} : {entity.Reference} {entity.IsMarkedForDeletion}");
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(ExceptionHelper.GetErrorMessage(exception));
            }
        }

        [TestMethod] public void Generate_Script_Cte()
        {
            filePath = "C:\\temp\\scripting-test\\cte\\04-script.txt";

            CreateScriptModel();

            if (_model == null)
            {
                return;
            }

            ScopeBuilder builder = new();

            if (!builder.TryBuild(in _model, out ScriptScope scope, out string error))
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

            if (!transformer.TryTransform(_model, out error))
            {
                Console.WriteLine(error);
                return;
            }

            MsSqlGenerator generator = new();

            if (!generator.TryGenerate(_model, out GeneratorResult result))
            {
                Console.WriteLine(result.Error);
                return;
            }

            Console.WriteLine();
            Console.WriteLine(filePath);
            Console.WriteLine();
            Console.WriteLine(result.Script);

            ShowEntityMap(result.Mapper);
        }
        [TestMethod] public void Generate_Offset_Fetch()
        {
            filePath = "C:\\temp\\scripting-test\\paging\\02-script.txt";

            CreateScriptModel();

            if (_model == null)
            {
                return;
            }

            ScopeBuilder builder = new();

            if (!builder.TryBuild(in _model, out ScriptScope scope, out string error))
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

            if (!transformer.TryTransform(_model, out error))
            {
                Console.WriteLine(error);
                return;
            }

            MsSqlGenerator generator = new();

            if (!generator.TryGenerate(_model, out GeneratorResult result))
            {
                Console.WriteLine(result.Error);
                return;
            }

            Console.WriteLine();
            Console.WriteLine(filePath);
            Console.WriteLine();
            Console.WriteLine(result.Script);

            ShowEntityMap(result.Mapper);
        }
        [TestMethod] public void Generate_Group_Having()
        {
            filePath = "C:\\temp\\scripting-test\\group-having\\01-script.txt";

            CreateScriptModel();

            if (_model == null)
            {
                return;
            }

            ScopeBuilder builder = new();

            if (!builder.TryBuild(in _model, out ScriptScope scope, out string error))
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

            if (!transformer.TryTransform(_model, out error))
            {
                Console.WriteLine(error);
                return;
            }

            MsSqlGenerator generator = new();

            if (!generator.TryGenerate(_model, out GeneratorResult result))
            {
                Console.WriteLine(result.Error);
                return;
            }

            Console.WriteLine();
            Console.WriteLine(filePath);
            Console.WriteLine();
            Console.WriteLine(result.Script);

            ShowEntityMap(result.Mapper);
        }

        [TestMethod] public void Execute_Destructive_Read()
        {
            string script;
            filePath = "C:\\temp\\scripting-test\\destructive-read\\00-script.txt";

            Console.WriteLine();
            Console.WriteLine(filePath);
            Console.WriteLine();

            using (StreamReader reader = new(filePath, Encoding.UTF8))
            {
                script = reader.ReadToEnd();
            }

            ScriptExecutor executor = new(_cache);
            executor.Parameters.Add("MessageCount", 10);

            try
            {
                foreach (var entity in executor.ExecuteReader<OutgoingMessage>(script))
                {
                    Console.WriteLine("*****");
                    ShowPropertyValues(entity);
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(ExceptionHelper.GetErrorMessage(exception));
            }
        }
        [TestMethod] public void Execute_Offset_Fetch()
        {
            string script;

            foreach (string filePath in Directory.GetFiles("C:\\temp\\scripting-test\\paging"))
            {
                Console.WriteLine();
                Console.WriteLine(filePath);
                Console.WriteLine();

                using (StreamReader reader = new(filePath, Encoding.UTF8))
                {
                    script = reader.ReadToEnd();
                }

                ScriptExecutor executor = new(_cache);
                executor.Parameters.Add("PageSize", 5);
                executor.Parameters.Add("PageNumber", 2);

                try
                {
                    foreach (var entity in executor.ExecuteReader<ProductInfo>(script))
                    {
                        Console.WriteLine("*****");
                        ShowPropertyValues(entity);
                    }
                }
                catch (Exception exception)
                {
                    Console.WriteLine(ExceptionHelper.GetErrorMessage(exception));
                }
            }
        }
        [TestMethod] public void Execute_Group_Having()
        {
            string script;

            foreach (string filePath in Directory.GetFiles("C:\\temp\\scripting-test\\group-having"))
            {
                Console.WriteLine();
                Console.WriteLine(filePath);
                Console.WriteLine();

                using (StreamReader reader = new(filePath, Encoding.UTF8))
                {
                    script = reader.ReadToEnd();
                }

                ScriptExecutor executor = new(_cache);
                executor.Parameters.Add("Amount", 7);

                try
                {
                    foreach (var entity in executor.ExecuteReader(script))
                    {
                        Console.WriteLine("*****");
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
    public class ProductInfo
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public EntityRef Reference { get; set; }
        public bool IsMarkedForDeletion { get; set; }
    }
    public sealed class NodeInfo
    {
        public string NodeCode { get; set; } = string.Empty;
        public EntityRef NodeRef { get; set; } = EntityRef.Empty;
        public bool UseKafka { get; set; } = false;
        public bool UseRabbitMQ { get; set; } = false;
        public override string ToString()
        {
            return $"[{NodeCode}] {{ \"Kafka\": {UseKafka.ToString().ToLowerInvariant()}, \"RabbitMQ\": {UseRabbitMQ.ToString().ToLowerInvariant()} }} {NodeRef}";
        }
    }
    public sealed class OutgoingMessage
    {
        public decimal МоментВремени { get; set; } = 0L;
        public Guid Идентификатор { get; set; } = Guid.Empty;
        public string Заголовки { get; set; } = string.Empty;
        public string Отправитель { get; set; } = string.Empty;
        public string Получатели { get; set; } = string.Empty;
        public string ТипСообщения { get; set; } = string.Empty;
        public string ТелоСообщения { get; set; } = string.Empty;
        public DateTime ВремяСоздания { get; set; } = DateTime.MinValue;
    }
}