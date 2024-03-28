using DaJet.Data;

namespace DaJet.Sqlite.Test
{
    [TestClass] public class SqliteMetadataProvider
    {
        private static readonly string DATABASE = "C:\\temp\\sqlite\\dajet.db";
        [TestMethod] public void Create_Database()
        {
            //SqliteDbConfigurator configurator = new(DATABASE);

            //if (!configurator.TryConfigureDatabase(out string error))
            //{
            //    Console.WriteLine(error); return;
            //}

            IDataSource source = new MetadataSource(DATABASE);

            Console.WriteLine(source.ConnectionString);
        }
    }
}