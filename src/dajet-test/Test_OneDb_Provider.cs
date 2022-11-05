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
    }
}