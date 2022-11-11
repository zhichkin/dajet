using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;

namespace DaJet.Data.Provider.Test
{
    [TestClass] public class Test_OneDb_Provider
    {
        private const string PG_CONNECTION_STRING = "Host=127.0.0.1;Port=5432;Database=dajet-metadata-pg;Username=postgres;Password=postgres;";
        private const string MS_CONNECTION_STRING = "Data Source=ZHICHKIN;Initial Catalog=dajet-metadata-ms;Integrated Security=True;Encrypt=False;";
        
        [TestMethod] public void PG_Connection()
        {
            Db_Connection(PG_CONNECTION_STRING);
        }
        [TestMethod] public void MS_Connection()
        {
            Db_Connection(MS_CONNECTION_STRING);
        }
        private void Db_Connection(string connectionString)
        {
            OneDbConnection connection;

            Stopwatch watch = new();
            watch.Start();

            using (connection = new(connectionString))
            {
                Console.WriteLine($"Database = {connection.Database}");
                connection.Open();
                Console.WriteLine($"Connection state = {connection.State}");
            }
            Console.WriteLine($"Connection state = {connection.State}");

            watch.Stop();
            Console.WriteLine($"Elapsed = {watch.ElapsedMilliseconds} ms");

            Console.WriteLine();

            watch.Reset();
            watch.Start();

            using (connection = new(connectionString))
            {
                Console.WriteLine($"Database = {connection.Database}");
                connection.Open();
                Console.WriteLine($"Connection state = {connection.State}");
            }
            Console.WriteLine($"Connection state = {connection.State}");

            watch.Stop();
            Console.WriteLine($"Elapsed = {watch.ElapsedMilliseconds} ms");
        }

        [TestMethod] public void PG_ExecuteScalar()
        {
            Db_ExecuteScalar(PG_CONNECTION_STRING);
        }
        [TestMethod] public void MS_ExecuteScalar()
        {
            Db_ExecuteScalar(MS_CONNECTION_STRING);
        }
        private void Db_ExecuteScalar(string connectionString)
        {
            string commandText =
                "ВЫБРАТЬ Наименование ИЗ Справочник.Номенклатура;";

            using (OneDbConnection connection = new(connectionString))
            {
                connection.Open();

                using (OneDbCommand command = connection.CreateCommand())
                {
                    command.CommandText = commandText;

                    object? result = command.ExecuteScalar();

                    Console.WriteLine($"Наименование = {result}");
                }
            }
        }

        [TestMethod] public void PG_ExecuteReader()
        {
            Db_ExecuteReader(PG_CONNECTION_STRING);
        }
        [TestMethod] public void MS_ExecuteReader()
        {
            Db_ExecuteReader(MS_CONNECTION_STRING);
        }
        private void Db_ExecuteReader(string connectionString)
        {
            string commandText =
                "ВЫБРАТЬ ПЕРВЫЕ 6 Ссылка, Код, Наименование, ПометкаУдаления, СоставнойТип ИЗ Справочник.Номенклатура;";

            using (OneDbConnection connection = new(connectionString))
            {
                connection.Open();

                using (OneDbCommand command = connection.CreateCommand())
                {
                    command.CommandText = commandText;

                    using (OneDbDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Console.WriteLine($"***");
                            Console.WriteLine($"| Номенклатура: {reader["Наименование"]}");
                            Console.WriteLine($"| -------------");

                            for (int ordinal = 0; ordinal < reader.FieldCount; ordinal++)
                            {
                                string name = reader.GetName(ordinal);
                                object value = reader.GetValue(ordinal);
                                Type type = reader.GetFieldType(ordinal);
                                string typeName = reader.GetDataTypeName(ordinal);

                                if (value is Union union)
                                {
                                    value = (union.IsEmpty ? "Неопределено" : union.Value);
                                }

                                Console.WriteLine($"| {name} = {value} [{type}] {typeName}");
                            }
                        }
                        reader.Close();
                    }
                }
            }
        }

        [TestMethod] public void PG_ExecuteReader_GetEntity()
        {
            Db_ExecuteReader_GetEntity(PG_CONNECTION_STRING);
        }
        [TestMethod] public void MS_ExecuteReader_GetEntity()
        {
            Db_ExecuteReader_GetEntity(MS_CONNECTION_STRING);
        }
        private void Db_ExecuteReader_GetEntity(string connectionString)
        {
            string commandText =
                "ВЫБРАТЬ ПЕРВЫЕ 5 " +
                "Ссылка, Код, " +
                "Наименование, ПометкаУдаления, " +
                "СоставнойТип, Валюта " +
                "ИЗ Справочник.Номенклатура " +
                "УПОРЯДОЧИТЬ ПО Код ВОЗР;";

            using (OneDbConnection connection = new(connectionString))
            {
                connection.Open();

                using (OneDbCommand command = connection.CreateCommand())
                {
                    command.CommandText = commandText;

                    using (OneDbDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Product product = reader.GetEntity<Product>();

                            Console.WriteLine(string.Format("{0}: {1} [{2}] {3} {4} [{5}]",
                                product.Код,
                                product.Наименование,
                                product.ПометкаУдаления,
                                product.Ссылка,
                                product.СоставнойТип,
                                product.Валюта));
                        }
                        reader.Close();
                    }
                }
            }
        }

        [TestMethod] public void PG_ExecuteReader_Parameter_Boolean()
        {
            Db_ExecuteReader_Parameter_Boolean(PG_CONNECTION_STRING);
        }
        [TestMethod] public void MS_ExecuteReader_Parameter_Boolean()
        {
            Db_ExecuteReader_Parameter_Boolean(MS_CONNECTION_STRING);
        }
        private void Db_ExecuteReader_Parameter_Boolean(string connectionString)
        {
            string commandText =
                "ВЫБРАТЬ Ссылка, Код, Наименование " +
                "ИЗ Справочник.Номенклатура " +
                "ГДЕ ПометкаУдаления = @ПометкаУдаления " +
                "УПОРЯДОЧИТЬ ПО Код ВОЗР;";

            Db_ExecuteReader_With_Parameter(connectionString, commandText, "ПометкаУдаления", true);
        }
        [TestMethod] public void PG_ExecuteReader_Parameter_Numeric()
        {
            Db_ExecuteReader_Parameter_Numeric(PG_CONNECTION_STRING);
        }
        [TestMethod] public void MS_ExecuteReader_Parameter_Numeric()
        {
            Db_ExecuteReader_Parameter_Numeric(MS_CONNECTION_STRING);
        }
        private void Db_ExecuteReader_Parameter_Numeric(string connectionString)
        {
            string commandText =
                "ВЫБРАТЬ Ссылка, Код, Наименование " +
                "ИЗ Справочник.Номенклатура " +
                "ГДЕ Реквизит2 <= @Параметр " +
                "УПОРЯДОЧИТЬ ПО Код ВОЗР;";

            Db_ExecuteReader_With_Parameter(connectionString, commandText, "Параметр", 3);
        }
        [TestMethod] public void PG_ExecuteReader_Parameter_DateTime()
        {
            Db_ExecuteReader_Parameter_DateTime(PG_CONNECTION_STRING);
        }
        [TestMethod] public void MS_ExecuteReader_Parameter_DateTime()
        {
            Db_ExecuteReader_Parameter_DateTime(MS_CONNECTION_STRING);
        }
        private void Db_ExecuteReader_Parameter_DateTime(string connectionString)
        {
            string commandText =
                "ВЫБРАТЬ Ссылка, Код, Наименование " +
                "ИЗ Справочник.Номенклатура " +
                "ГДЕ РеквизитДата = @Параметр " +
                "УПОРЯДОЧИТЬ ПО Код ВОЗР;";

            Db_ExecuteReader_With_Parameter(connectionString, commandText, "Параметр", new DateTime(2022, 11, 3));
        }
        [TestMethod] public void PG_ExecuteReader_Parameter_String()
        {
            Db_ExecuteReader_Parameter_String(PG_CONNECTION_STRING);
        }
        [TestMethod] public void MS_ExecuteReader_Parameter_String()
        {
            Db_ExecuteReader_Parameter_String(MS_CONNECTION_STRING);
        }
        private void Db_ExecuteReader_Parameter_String(string connectionString)
        {
            string commandText =
                "ВЫБРАТЬ Ссылка, Код, Наименование " +
                "ИЗ Справочник.Номенклатура " +
                "ГДЕ Наименование = @Наименование " +
                "УПОРЯДОЧИТЬ ПО Код ВОЗР;";

            Db_ExecuteReader_With_Parameter(connectionString, commandText, "Наименование", "Товар 05");
        }
        [TestMethod] public void PG_ExecuteReader_Parameter_Uuid()
        {
            Db_ExecuteReader_Parameter_Uuid(PG_CONNECTION_STRING);
        }
        [TestMethod] public void MS_ExecuteReader_Parameter_Uuid()
        {
            Db_ExecuteReader_Parameter_Uuid(MS_CONNECTION_STRING);
        }
        private void Db_ExecuteReader_Parameter_Uuid(string connectionString)
        {
            string commandText =
                "ВЫБРАТЬ Ссылка, Код, Наименование " +
                "ИЗ Справочник.Номенклатура " +
                "ГДЕ Валюта = @Валюта " +
                "УПОРЯДОЧИТЬ ПО Код ВОЗР;";

            Db_ExecuteReader_With_Parameter(connectionString, commandText, "Валюта", new Guid("b82c5d0d-613e-11ed-9cde-408d5c93cc8e"));
        }
        [TestMethod] public void PG_ExecuteReader_Parameter_Entity()
        {
            Db_ExecuteReader_Parameter_Entity(PG_CONNECTION_STRING);
        }
        [TestMethod] public void MS_ExecuteReader_Parameter_Entity()
        {
            Db_ExecuteReader_Parameter_Entity(MS_CONNECTION_STRING);
        }
        private void Db_ExecuteReader_Parameter_Entity(string connectionString)
        {
            string commandText =
                "ВЫБРАТЬ Ссылка, Код, Наименование " +
                "ИЗ Справочник.Номенклатура " +
                "ГДЕ Валюта = @Валюта " +
                "УПОРЯДОЧИТЬ ПО Код ВОЗР;";

            Db_ExecuteReader_With_Parameter(connectionString, commandText,
                "Валюта", new Entity(842, new Guid("987b0650-613d-11ed-9cde-408d5c93cc8e")));
        }
        private void Db_ExecuteReader_With_Parameter(string connectionString, string commandText, string patameterName, object parameterValue)
        {
            using (OneDbConnection connection = new(connectionString))
            {
                connection.Open();

                using (OneDbCommand command = connection.CreateCommand())
                {
                    command.CommandText = commandText;
                    command.Parameters.AddWithValue(patameterName, parameterValue);

                    using (OneDbDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Product product = reader.GetEntity<Product>();

                            Console.WriteLine(string.Format("[{0}] {1} {2}",
                                product.Код,
                                product.Ссылка,
                                product.Наименование));
                        }
                        reader.Close();
                    }
                }
            }
        }
    }

    internal sealed class Product
    {
        public Entity Ссылка { get; set; } = Entity.Empty;
        public string Код { get; set; } = string.Empty;
        public string Наименование { get; set; } = string.Empty;
        public bool ПометкаУдаления { get; set; }
        public Entity Валюта { get; set; } = Entity.Empty;
        public Union СоставнойТип { get; set; } = Union.Empty;
    }
}