using DaJet.Data;
using DaJet.Metadata;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using DaJet.Metadata.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.Text;

namespace DaJet.Metadata.Test
{
    [TestClass] public class Compare_Metadata_To_Database
    {
        //private const string MS_CONNECTION_STRING = "Data Source=ZHICHKIN;Initial Catalog=trade_11_2_3_159_demo;Integrated Security=True;Encrypt=False;";
        //private const string MS_CONNECTION_STRING = "Data Source=ZHICHKIN;Initial Catalog=accounting_3_0_72_72_demo;Integrated Security=True;Encrypt=False;";
        //private const string MS_CONNECTION_STRING = "Data Source=ZHICHKIN;Initial Catalog=test_node_1;Integrated Security=True;Encrypt=False;";
        //private const string PG_CONNECTION_STRING = "Host=127.0.0.1;Port=5432;Database=test_node_2;Username=postgres;Password=postgres;";
        private const string MS_CONNECTION_STRING = "Data Source=ZHICHKIN;Initial Catalog=dajet-metadata-ms;Integrated Security=True;Encrypt=False;";
        private const string PG_CONNECTION_STRING = "Host=127.0.0.1;Port=5432;Database=dajet-metadata-pg;Username=postgres;Password=postgres;";
        private InfoBase _infoBase;
        private readonly MetadataService _service = new();
        ISqlMetadataReader _database = new SqlMetadataReader();
        IMetadataCompareAndMergeService _comparator = new MetadataCompareAndMergeService(); // comparator
        [TestMethod] public void MS_Test()
        {
            InfoBaseOptions options = new()
            {
                Key = "test",
                ConnectionString = MS_CONNECTION_STRING,
                DatabaseProvider = DatabaseProvider.SqlServer
            };

            _service.Add(options);
            
            _database.UseDatabaseProvider(options.DatabaseProvider);
            _database.UseConnectionString(options.ConnectionString);

            if (!_service.TryGetInfoBase("test", out _infoBase, out string error))
            {
                Console.WriteLine($"Failed to open info base: {error}");
                return;
            }
            
            Run_Test();
        }
        [TestMethod] public void PG_Test()
        {
            InfoBaseOptions options = new()
            {
                Key = "test",
                ConnectionString = PG_CONNECTION_STRING,
                DatabaseProvider = DatabaseProvider.PostgreSql
            };

            _service.Add(options);

            _database.UseDatabaseProvider(options.DatabaseProvider);
            _database.UseConnectionString(options.ConnectionString);

            if (!_service.TryGetInfoBase("test", out _infoBase, out string error))
            {
                Console.WriteLine($"Failed to open info base: {error}");
                return;
            }

            Run_Test();
        }
        private void Run_Test()
        {
            Console.WriteLine($"{_infoBase.Name} - {_infoBase.Alias} [{_infoBase.Comment}] {_infoBase.AppConfigVersion}");

            Dictionary<Guid, string> types = new()
            {
                { MetadataTypes.Catalog, "C:\\temp\\db-tests\\catalogs.txt" },
                { MetadataTypes.Document, "C:\\temp\\db-tests\\documents.txt" },
                { MetadataTypes.Enumeration, "C:\\temp\\db-tests\\enumerations.txt" },
                { MetadataTypes.Publication, "C:\\temp\\db-tests\\publications.txt" },
                { MetadataTypes.Characteristic, "C:\\temp\\db-tests\\characteristics.txt" },
                { MetadataTypes.InformationRegister, "C:\\temp\\db-tests\\information-registers.txt" },
                { MetadataTypes.AccumulationRegister, "C:\\temp\\db-tests\\accumulation-registers.txt" }
            };

            foreach (var type in types)
            {
                Run_Test(type.Key, type.Value);
            }
        }
        private void Run_Test(Guid metadataType, string outputFile)
        {
            if (!_service.TryGetMetadataCache("test", out MetadataCache cache, out string error))
            {
                Console.WriteLine($"Failed to get cache: {error}");
                return;
            }

            int count = 0;
            List<string> delete;
            List<string> insert;

            using (StreamWriter stream = new(outputFile, false, Encoding.UTF8))
            {
                foreach (MetadataItem item in cache.GetMetadataItems(metadataType))
                {
                    MetadataObject metadata = cache.GetMetadataObject(item);

                    if (metadata is not ApplicationObject @object)
                    {
                        stream.WriteLine($"NOT FOUND: {item}");
                        continue;
                    }

                    count++;

                    bool result = CompareWithDatabase(@object, out delete, out insert);

                    if (!result)
                    {
                        WriteToLogFile(stream, @object, delete, insert);
                    }

                    if (@object is not ITablePartOwner aggregate)
                    {
                        continue;
                    }

                    foreach (TablePart tablePart in aggregate.TableParts)
                    {
                        result = CompareWithDatabase(tablePart, out delete, out insert);

                        if (!result)
                        {
                            WriteToLogFile(stream, tablePart, delete, insert);
                        }
                    }
                }
                stream.WriteLine("*******************************");
                stream.WriteLine(count.ToString() + " objects processed.");
            }
        }
        private bool CompareWithDatabase(ApplicationObject @object, out List<string> delete, out List<string> insert)
        {
            delete = new List<string>();
            insert = new List<string>();

            List<SqlFieldInfo> sqlFields = _database.GetSqlFieldsOrderedByName(@object.TableName);

            if (sqlFields.Count == 0)
            {
                return false;
            }

            List<string> targetFields = _comparator.PrepareComparison(@object.Properties);
            List<string> sourceFields = _comparator.PrepareComparison(sqlFields);

            _comparator.Compare(targetFields, sourceFields, out delete, out insert);

            return (delete.Count + insert.Count) == 0;
        }
        private void WriteToLogFile(StreamWriter stream, ApplicationObject @object, List<string> delete, List<string> insert)
        {
            stream.WriteLine("\"" + @object.Name + "\" (" + @object.TableName + "):");

            if (delete.Count > 0)
            {
                stream.WriteLine("  Delete fields:");
                foreach (string field in delete)
                {
                    stream.WriteLine("   - " + field);
                }
            }

            if (insert.Count > 0)
            {
                stream.WriteLine("  Insert fields:");
                foreach (string field in insert)
                {
                    stream.WriteLine("   - " + field);
                }
            }
        }

        [TestMethod] public void Test_Generics()
        {
            string IB_KEY = "test";

            _service.Add(new InfoBaseOptions()
            {
                Key = IB_KEY,
                ConnectionString = MS_CONNECTION_STRING,
                DatabaseProvider = DatabaseProvider.SqlServer
            });

            Stopwatch watch = new();
            watch.Start();

            if (!_service.TryGetMetadataCache(IB_KEY, out MetadataCache cache, out string error))
            {
                Console.WriteLine($"Failed to get cache: {error}");
                return;
            }

            watch.Stop();
            Console.WriteLine($"Initialize cache: elapsed in {watch.ElapsedMilliseconds} ms");

            watch.Restart();

            int count = 0;
            foreach (var item in cache.GetMetadataItems(MetadataTypes.Catalog))
            {
                count++;
                Catalog catalog = cache.GetMetadataObject<Catalog>(item.Uuid);
                //Console.WriteLine($"[{catalog.TableName}] {catalog.Name} : {catalog.Alias}");
            }

            watch.Stop();
            Console.WriteLine($"Read {count} catalogs: elapsed in {watch.ElapsedMilliseconds} ms");
        }

        [TestMethod] public void Show_Catalogs()
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

            // Читаем метаданные справочников
            foreach (MetadataItem item in cache.GetMetadataItems(MetadataTypes.Catalog))
            {
                Catalog catalog = cache.GetMetadataObject<Catalog>(item.Uuid);

                // Имя справочника и его таблицы СУБД
                Console.WriteLine($"{catalog.Name} [{catalog.TableName}]");

                // Читаем метаданные свойств справочников
                foreach (MetadataProperty property in catalog.Properties)
                {
                    // Имя свойства справочника
                    Console.WriteLine(property.Name);

                    // Читаем метаданные колонок таблицы СУБД
                    foreach (MetadataColumn column in property.Columns)
                    {
                        // Имя колонки таблицы СУБД
                        Console.WriteLine(column.Name);
                    }
                }
            }
        }

        [TestMethod] public void Show_MetadataObject()
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

            // Получаем объект метаданных по его полному имени
            InformationRegister entity = cache.GetMetadataObject<InformationRegister>("РегистрСведений.ТестовыйРегистр");

            if (entity == null)
            {
                Console.WriteLine("Объект метаданных не найден.");
                return;
            }

            // Имя справочника и его таблицы СУБД
            Console.WriteLine($"{entity.Name} [{entity.TableName}]");

            // Читаем метаданные свойств справочников
            foreach (MetadataProperty property in entity.Properties)
            {
                // Имя свойства справочника
                Console.WriteLine(property.Name);

                // Читаем метаданные колонок таблицы СУБД
                foreach (MetadataColumn column in property.Columns)
                {
                    // Имя колонки таблицы СУБД
                    Console.WriteLine(column.Name);
                }
            }
        }
    }
}