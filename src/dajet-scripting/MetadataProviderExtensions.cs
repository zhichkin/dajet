using DaJet.Data;
using DaJet.Metadata;

namespace DaJet.Scripting
{
    public static class MetadataProviderExtensions
    {
        /// <summary>
        /// Formats parameters for the .NET data provider (database)
        /// </summary>
        public static void ConfigureDbParameters(this IMetadataProvider context, in Dictionary<string, object> parameters)
        {
            foreach (var parameter in parameters)
            {
                if (parameter.Value is Entity entity)
                {
                    parameters[parameter.Key] = entity.Identity.ToByteArray();
                }
                else if (parameter.Value is bool boolean)
                {
                    if (context.DatabaseProvider == DatabaseProvider.SqlServer)
                    {
                        parameters[parameter.Key] = new byte[] { Convert.ToByte(boolean) };
                    }
                }
                else if (parameter.Value is DateTime dateTime)
                {
                    parameters[parameter.Key] = dateTime.AddYears(context.YearOffset);
                }
                else if (parameter.Value is Guid uuid)
                {
                    parameters[parameter.Key] = uuid.ToByteArray();
                }
                //TODO: user-defined type - table-valued parameter
                //else if (parameter.Value is List<DataObject> table)
                //{
                //    DeclareStatement declare = GetDeclareStatementByName(in model, parameter.Key);

                //    parameters[parameter.Key] = new TableValuedParameter()
                //    {
                //        Name = parameter.Key,
                //        Value = table,
                //        DbName = declare is null ? string.Empty : declare.Type.Identifier
                //    };
                //}
            }
        }
    }
}