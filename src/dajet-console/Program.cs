using DaJet.Data;
using DaJet.Data.Provider;
using DaJet.Metadata;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using System.Reflection;

namespace DaJet_Console
{
    internal static class Program
    {
        private static readonly MetadataService _metadata = new();
        private static MetadataCache _pg_cache;
        private static MetadataCache _ms_cache;
        private const string PG_CONNECTION_STRING = "Host=127.0.0.1;Port=5432;Database=dajet-metadata-pg;Username=postgres;Password=postgres;";
        private const string MS_CONNECTION_STRING = "Data Source=ZHICHKIN;Initial Catalog=dajet-metadata-ms;Integrated Security=True;Encrypt=False;";
        static void Main()
        {
            // Этот код необязателен (!) для использования провайдера данных.
            // OneDbConnection выполняет его автоматически при вызове метода Open (!)
            // Эта строка нужна только для процедуры Использование_Типа_Entity (см. ниже)
            InitializeMetadataCache();

            // *******************************************
            // * Примеры использования провайдера данных *
            // *******************************************

            //Базовый_Пример_Использования();
            //Использование_Типа_Entity();
            //Использование_Типа_Union();
            //Отображение_Запроса_На_Класс();
            //Использование_Параметров_Запроса();

            WriteConfigFile();
            //TestDataRecordJsonConverter();
        }
        private static void InitializeMetadataCache()
        {
            _metadata.Add(new InfoBaseOptions()
            {
                Key = "pg-db",
                ConnectionString = PG_CONNECTION_STRING,
                DatabaseProvider = DatabaseProvider.PostgreSql
            });

            _metadata.Add(new InfoBaseOptions()
            {
                Key = "ms-db",
                ConnectionString = MS_CONNECTION_STRING,
                DatabaseProvider = DatabaseProvider.SqlServer
            });

            if (!_metadata.TryGetMetadataCache("pg-db", out _pg_cache, out string error))
            {
                Console.WriteLine($"[PG] Ошибка загрузки метаданных: {error}");
            }

            if (!_metadata.TryGetMetadataCache("ms-db", out _ms_cache, out error))
            {
                Console.WriteLine($"[MS] Ошибка загрузки метаданных: {error}");
            }
        }
        private static void Базовый_Пример_Использования()
        {
            string commandText =
                "ВЫБРАТЬ ПЕРВЫЕ 1 " +
                "Ссылка, Код, Наименование, ПометкаУдаления, " +
                "РеквизитДата, РеквизитЧисло, СоставнойТип, Валюта " +
                "ИЗ Справочник.Номенклатура;";

            using (OneDbConnection connection = new(PG_CONNECTION_STRING))
            {
                connection.Open();

                using (OneDbCommand command = connection.CreateCommand())
                {
                    command.CommandText = commandText;

                    using (OneDbDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            for (int ordinal = 0; ordinal < reader.FieldCount; ordinal++)
                            {
                                Console.WriteLine(string.Format(" {0,-16} {1,-9} {2,-42} {3}",
                                    reader.GetName(ordinal),         // Имя поля выборки
                                    reader.GetDataTypeName(ordinal), // Тип данных значения
                                    reader.GetValue(ordinal),        // Значение поля выборки
                                    reader.GetFieldType(ordinal)));  // Полное имя типа данных
                            }
                        }
                    }
                }
            }
        }
        private static void Использование_Типа_Entity()
        {
            string commandText = "ВЫБРАТЬ ПЕРВЫЕ 1 Ссылка ИЗ Справочник.Валюты;";

            using (OneDbConnection connection = new(MS_CONNECTION_STRING))
            {
                connection.Open();

                using (OneDbCommand command = connection.CreateCommand())
                {
                    command.CommandText = commandText;

                    using (OneDbDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Entity entity = (Entity)reader["Ссылка"];
                            MetadataItem item = _ms_cache.GetMetadataItem(entity.TypeCode);
                            string typeName = MetadataTypes.ResolveName(item.Type);

                            Console.WriteLine($" Ссылка = {entity}");
                            Console.WriteLine($" [{entity.TypeCode}] = {typeName}.{item.Name}");
                        }
                    }
                }
            }
        }
        private static void Использование_Типа_Union()
        {
            string commandText = "ВЫБРАТЬ ПЕРВЫЕ 6 Код, СоставнойТип ИЗ Справочник.Номенклатура;";

            using (OneDbConnection connection = new(MS_CONNECTION_STRING))
            {
                connection.Open();

                using (OneDbCommand command = connection.CreateCommand())
                {
                    command.CommandText = commandText;

                    using (OneDbDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string code = (string)reader.GetValue(0);
                            Union union = (Union)reader["СоставнойТип"];

                            if (union.IsUndefined)
                            {
                                Console.WriteLine($" {code}: [{union.Tag}] = Неопределено");
                            }
                            else
                            {
                                Console.WriteLine($" {code}: [{union.Tag}] = {union.Value}");
                            }
                        }
                    }
                }
            }
        }
        private static void Отображение_Запроса_На_Класс()
        {
            string commandText =
                "ВЫБРАТЬ ПЕРВЫЕ 1 " +
                "Ссылка, Код, Наименование, ПометкаУдаления, Валюта, СоставнойТип " +
                "ИЗ Справочник.Номенклатура;";

            using (OneDbConnection connection = new(MS_CONNECTION_STRING))
            {
                connection.Open();

                using (OneDbCommand command = connection.CreateCommand())
                {
                    command.CommandText = commandText;

                    using (OneDbDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Product product = reader.Map<Product>();

                            foreach (PropertyInfo property in typeof(Product).GetProperties())
                            {
                                object? value = property.GetValue(product, null);

                                Console.WriteLine($" {property.Name} = {value}");
                            }
                        }
                    }
                }
            }
        }
        private static void Использование_Параметров_Запроса()
        {
            string commandText =
                "ВЫБРАТЬ Код, Наименование, РеквизитДата " +
                "ИЗ Справочник.Номенклатура " +
                "ГДЕ РеквизитДата <= @ПараметрДата " +
                "УПОРЯДОЧИТЬ ПО РеквизитДата УБЫВ;";

            using (OneDbConnection connection = new(PG_CONNECTION_STRING))
            {
                connection.Open();

                using (OneDbCommand command = connection.CreateCommand())
                {
                    command.CommandText = commandText;

                    command.Parameters.AddWithValue("ПараметрДата", new DateTime(2022, 11, 3));

                    Product product = new(); // buffer instance

                    using (OneDbDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            reader.Map<Product>(in product); // Ещё один способ использования метода Map

                            Console.WriteLine($" {product.Код} [{product.РеквизитДата}] {product.Наименование}");
                        }
                        reader.Close();
                    }
                }
            }
        }

        private static void WriteConfigFile()
        {
            IMetadataService service = new MetadataService();
            service.Add(new InfoBaseOptions()
            {
                Key = "dajet-metadata-ms",
                ConnectionString = MS_CONNECTION_STRING,
                DatabaseProvider = DatabaseProvider.SqlServer
            });

            if (!service.TryGetMetadataProvider("dajet-metadata-ms", out IMetadataProvider provider, out string error))
            {
                Console.WriteLine(error);
                _ = Console.ReadKey(false);
                return;
            }

            MetadataObject metadata = provider.GetMetadataObject("РегистрСведений.ЦеныНоменклатуры");

            Console.WriteLine(metadata.Uuid);

            using (ConfigFileReader reader = new(DatabaseProvider.SqlServer, MS_CONNECTION_STRING, ConfigTables.Config, metadata.Uuid))
            {
                ConfigObject configObject = new ConfigFileParser().Parse(reader);

                new ConfigFileWriter().Write(configObject, "C:\\temp\\1c-dump\\РегистрСведений.ЦеныНоменклатуры.txt");
            }

            Console.WriteLine("Press any key to exit...");
            _ = Console.ReadKey(false);
        }

        //private static void TestDataRecordJsonConverter()
        //{
        //    string message = @"{ ""Ссылка"": ""{1072:8d40de9c-935c-8ecc-11ed-63721997a442}"", ""Узел"": ""{1255:8d40ed9c-935c-8ecc-11ee-06117c486156}"", ""Данные"": [ { ""Код"": ""0001"", ""Наименование"": ""(ms) 01 тест"", ""ПометкаУдаления"": false }, { ""Код"": ""0002"", ""Наименование"": ""(ms) 02 тест"", ""ПометкаУдаления"": true } ], ""Узлы"": [ { ""Код"": ""DaJet"", ""Наименование"": ""Интеграция DaJet"" }, { ""Код"": ""TEST"", ""Наименование"": ""Интеграция TEST"" } ] }";

        //    byte[] buffer = Encoding.UTF8.GetBytes(message);

        //    Utf8JsonReader reader = new(buffer.AsSpan(), true, default);

        //    DataRecordJsonConverter _converter = new();
        //    JsonSerializerOptions JsonOptions = new()
        //    {
        //        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        //    };

        //    IDataRecord record = _converter.Read(ref reader, typeof(IDataRecord), JsonOptions);

        //    JsonWriterOptions JsonWriterOptions = new()
        //    {
        //        Indented = true,
        //        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        //    };

        //    using (MemoryStream memory = new())
        //    {
        //        using (Utf8JsonWriter writer = new(memory, JsonWriterOptions))
        //        {
        //            _converter.Write(writer, record, null);

        //            writer.Flush();

        //            Console.WriteLine(Encoding.UTF8.GetString(memory.ToArray()));
        //        }
        //    }
        //}
    }
    internal sealed class Product
    {
        public Entity Ссылка { get; set; } = Entity.Undefined;
        public string Код { get; set; } = string.Empty;
        public string Наименование { get; set; } = string.Empty;
        public bool ПометкаУдаления { get; set; }
        public Entity Валюта { get; set; } = Entity.Undefined;
        public Union СоставнойТип { get; set; } = Union.Undefined;
        public DateTime РеквизитДата { get; set; } = DateTime.MinValue;
    }
}