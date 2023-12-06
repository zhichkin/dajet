using DaJet.Metadata;

namespace DaJet.Data.Client
{
    public static class MetadataProviderExtensions
    {
        public static DataObject GetDataObject(this IMetadataProvider context, Entity entity)
        {
            DataObject root = null; // reference object

            int typeCode = entity.TypeCode;
            string typeName = context.GetMetadataItem(typeCode).ToString();

            string script = ScriptGenerator.GenerateSelectEntityScript(in context, entity);

            using (OneDbConnection connection = new(context))
            {
                connection.Open();

                using (OneDbCommand command = connection.CreateCommand())
                {
                    command.CommandText = script;
                    command.Parameters.Add("Ссылка", entity);

                    using (OneDbDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read()) // reference object main table
                        {
                            root = new DataObject(reader.FieldCount); //THINK: capacity + table parts count 
                            root.SetCodeAndName(typeCode, typeName);
                            reader.Map(in root);
                        }

                        while (reader.NextResult()) // table parts of the reference object
                        {
                            List<DataObject> table = new();
                            while (reader.Read())
                            {
                                DataObject record = new(reader.FieldCount);
                                reader.Map(in record);
                                table.Add(record);
                            }
                            root.SetValue(reader.Mapper.Name, table); //NOTE: this increments capacity of the root
                        }
                        reader.Close();
                    }
                }
            }
            
            return root;
        }
    }
}