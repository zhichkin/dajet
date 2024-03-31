using DaJet.Data;
using DaJet.Json;
using DaJet.Metadata;
using DaJet.Metadata.Model;
using DaJet.Scripting;
using Microsoft.Data.Sqlite;
using System.Data;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace DaJet.Sqlite.Test
{
    [TestClass] public class SqliteMetadataProvider
    {
        private static readonly string DATABASE = "C:\\temp\\sqlite\\dajet.db";
        [TestMethod] public void Create_Database()
        {
            SqliteDbConfigurator configurator = new(DATABASE);

            if (!configurator.TryConfigureDatabase(out string error))
            {
                Console.WriteLine(error); return;
            }

            IDataSource source = new MetadataSource(DATABASE);

            Console.WriteLine(source.ConnectionString);
        }
        [TestMethod] public void Create_Entity()
        {
            IDataSource source = new MetadataSource(in DATABASE);

            Sqlite.SqliteMetadataProvider metadata = new(in DATABASE);

            IDbConfigurator configurator = metadata.GetDbConfigurator();

            if (configurator is SqliteDbConfigurator sqlite)
            {
                int code = sqlite.GetNextSequenceValue();

                EntityRecord entity = source.Model.New<EntityRecord>();
                entity.Type = 1; // entity
                entity.Code = code; // type code - discriminator
                entity.Name = "Тестовый";
                entity.Table = $"test{code}";
                source.Create(entity);

                PropertyRecord property = source.Model.New<PropertyRecord>();
                property.Owner = entity.GetEntity(); // entity
                property.Name = "Ссылка";
                property.Type = "r";
                property.Discriminator = entity.Code;
                property.Column = "oid";
                property.PrimaryKey = 1;
                source.Create(property);

                property = source.Model.New<PropertyRecord>();
                property.Owner = entity.GetEntity(); // entity
                property.Name = "ВерсияДанных";
                property.Type = "n";
                property.Precision = 10;
                property.Scale = 0;
                property.IsSigned = true;
                property.Column = "version";
                property.IsReadOnly = true;
                source.Create(property);

                property = source.Model.New<PropertyRecord>();
                property.Owner = entity.GetEntity(); // entity
                property.Name = "Наименование";
                property.Column = "name";
                property.Type = "s";
                property.Length = 10;
                source.Create(property);
            }

            Console.WriteLine("Created");
        }
        [TestMethod] public void Create_Table()
        {
            IDataSource source = new MetadataSource(in DATABASE);

            EntityRecord entity = source.Select<EntityRecord>("Тестовый");

            Sqlite.SqliteMetadataProvider metadata = new(in DATABASE);

            IDbConfigurator configurator = metadata.GetDbConfigurator();

            if (configurator is SqliteDbConfigurator sqlite)
            {
                sqlite.CreateTable(in entity);
            }

            Console.WriteLine("Created");
        }
        [TestMethod] public void Select_Entity()
        {
            IMetadataProvider metadata = new Sqlite.SqliteMetadataProvider(in DATABASE);

            MetadataObject entity = metadata.GetMetadataObject("Тестовый");

            Console.WriteLine(entity.Name);

            string script = "SELECT Ссылка, Наименование, ВерсияДанных FROM Тестовый";

            Dictionary<string, object> parameters = new();

            if (!ScriptProcessor.TryProcess(in metadata, in script, in parameters, out TranspilerResult result, out string error))
            {
                Console.WriteLine(error); return;
            }

            string connectionString = new SqliteConnectionStringBuilder()
            {
                DataSource = DATABASE, Mode = SqliteOpenMode.ReadWriteCreate
            }
            .ToString();

            List<DataObject> table = new();

            using (SqliteConnection connection = new(connectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = result.Statements[0].Script;

                    using (IDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            DataObject record = new(result.Mappers[0].Properties.Count);

                            result.Mappers[0].Map(in reader, in record);

                            table.Add(record);
                        }
                        reader.Close();
                    }
                }
            }

            JsonSerializerOptions JsonOptions = new()
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };
            JsonOptions.Converters.Add(new DataObjectJsonConverter());
            JsonOptions.Converters.Add(new DictionaryJsonConverter());

            foreach (DataObject record in table)
            {
                string json = JsonSerializer.Serialize(record, JsonOptions);

                Console.WriteLine(json);
            }
        }
    }
}