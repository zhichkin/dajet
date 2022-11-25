using DaJet.Data;
using DaJet.Data.Provider;
using DaJet.Metadata;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaJet.Test_ValueStorage
{
    [TestClass] public class Test_ValueStorage
    {
        private const string MS_ERP_INFOBASE = "Data Source=ZHICHKIN;Initial Catalog=cerberus;Integrated Security=True;Encrypt=False;";
        private const string PG_CONNECTION_STRING = "Host=127.0.0.1;Port=5432;Database=dajet-metadata-pg;Username=postgres;Password=postgres;";
        private const string MS_CONNECTION_STRING = "Data Source=ZHICHKIN;Initial Catalog=dajet-metadata-ms;Integrated Security=True;Encrypt=False;";

        private readonly MetadataCache _cache;
        private readonly MetadataService _service;
        
        public Test_ValueStorage()
        {
            _service = new MetadataService();

            _service.Add(new InfoBaseOptions()
            {
                Key = "test",
                ConnectionString = MS_ERP_INFOBASE, // PG_CONNECTION_STRING
                DatabaseProvider = DatabaseProvider.SqlServer // DatabaseProvider.PostgreSql
            });

            if (!_service.TryGetMetadataCache("test", out _cache, out string error))
            {
                Console.WriteLine(error);
                return;
            }

            //Console.WriteLine(_cache.GetEnumValue("Справочник.СтавкиНДС.БезНДС"));
            //Console.WriteLine(_cache.GetEnumValue("Перечисление.ИсточникиПравилДляОбменаДанными.МакетКонфигурации"));
        }
        [TestMethod] public void GetValueStorage_MetadataCache()
        {
            using (OneDbConnection connection = new(_cache))
            {
                GetValueStorage(in connection);
            }
        }
        [TestMethod] public void GetValueStorage_ConnectionString()
        {
            using (OneDbConnection connection = new(MS_ERP_INFOBASE))
            {
                GetValueStorage(in connection);
            }
        }
        public void GetValueStorage(in OneDbConnection connection)
        {
            connection.Open();

            using (OneDbCommand command = connection.CreateCommand())
            {
                command.CommandText =
                    "ВЫБРАТЬ " +
                    "ИмяПланаОбмена, ВидПравил, ИмяМакетаПравил, " +
                    "ИсточникПравил, ИнформацияОПравилах, ПравилаXML " +
                    "ИЗ РегистрСведений.ПравилаДляОбменаДанными " +
                    "ГДЕ ИмяПланаОбмена = @ИмяПланаОбмена" +
                    " И ИмяМакетаПравил = @ИмяМакетаПравил;";

                command.Parameters.AddWithValue("ИмяПланаОбмена", "ОбменСTMS");
                //command.Parameters.AddWithValue("ИмяМакетаПравил", "ПравилаОбмена");
                command.Parameters.AddWithValue("ИмяМакетаПравил", "ПравилаРегистрации");

                //command.Parameters.AddWithValue("ИмяПланаОбмена", "test");
                //command.Parameters.AddWithValue("ИмяМакетаПравил", "");

                using (OneDbDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        object value = reader.GetValue(5);

                        if (value is byte[] data)
                        {
                            ValueStorage storage = new(data);

                            string content = string.Empty;

                            using (StreamReader stream = storage.GetDataAsStream())
                            {
                                if (storage.ValueType == SystemType.BinaryData)
                                {
                                    content = stream.ReadToEnd();
                                }
                            }

                            Console.WriteLine(content);
                        }
                    }
                }
            }
        }
    }
}