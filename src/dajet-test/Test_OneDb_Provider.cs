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
                "ВЫБРАТЬ ПЕРВЫЕ 5 Ссылка, Код, Наименование, ПометкаУдаления ИЗ Справочник.Номенклатура;";

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

                            Console.WriteLine(string.Format("{0}: {1} [{2}] {3}",
                                product.Код,
                                product.Наименование,
                                product.ПометкаУдаления,
                                product.Ссылка));
                        }
                        reader.Close();
                    }
                }
            }
        }
    }

    internal sealed class Product
    {
        public EntityRef Ссылка { get; set; } = EntityRef.Empty;
        public string Код { get; set; } = string.Empty;
        public string Наименование { get; set; } = string.Empty;
        public bool ПометкаУдаления { get; set; }
    }
}