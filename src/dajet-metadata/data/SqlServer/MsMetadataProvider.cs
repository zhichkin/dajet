using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace DaJet.Metadata.SqlServer
{
    public sealed class MsMetadataProvider : IMetadataProvider
    {
        private string _connectionString;
        public void Configure(in Dictionary<string, string> options)
        {
            SqlConnectionStringBuilder builder = new();

            Type type = typeof(SqlConnectionStringBuilder);

            foreach (var option in options)
            {
                if (option.Key == "host") { builder.DataSource = option.Value; }
                else if (option.Key == "dbname") { builder.InitialCatalog = option.Value; }
                else if (option.Key == "user") { builder.UserID = option.Value; }
                else if (option.Key == "pswd") { builder.Password = option.Value; }
                else
                {
                    PropertyInfo property = type.GetProperty(option.Key);
                    
                    if (property is not null)
                    {
                        if (property.PropertyType == typeof(int))
                        {
                            property.SetValue(builder, int.Parse(option.Value));
                        }
                        else if (property.PropertyType == typeof(bool))
                        {
                            property.SetValue(builder, bool.Parse(option.Value));
                        }
                        else if (property.PropertyType == typeof(string))
                        {
                            property.SetValue(builder, option.Value);
                        }
                    }
                }
            }
            
            _connectionString = builder.ToString();
        }
    }
}