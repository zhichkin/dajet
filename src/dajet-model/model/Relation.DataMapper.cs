using Microsoft.Data.SqlClient;
using System;
using System.Data;

namespace DaJet.Orm
{
    public partial class Relation
    {
        //public sealed class DataMapper : IDataMapper
        //{
        //    //private const string SelectCommandText = string.Empty;
        //    private const string InsertCommandText =
        //        @"INSERT [metadata].[relations] ([property], [entity]) VALUES (@property, @entity); SELECT @@ROWCOUNT;";
        //    private const string UpdateCommandText =
        //        @"UPDATE [metadata].[relations]" +
        //        @" SET [property] = @new_property, [entity] = @new_entity" +
        //        @" WHERE [property] = @old_property AND [entity] = @old_entity; " +
        //        @"SELECT @@ROWCOUNT;";
        //    private const string DeleteCommandText =
        //        @"DELETE [metadata].[relations] WHERE [property] = @old_property AND [entity] = @old_entity; SELECT @@ROWCOUNT;";

        //    private readonly string ConnectionString;
        //    private readonly IReferenceObjectFactory Factory;

        //    private DataMapper() { }
        //    public DataMapper(string connectionString, IReferenceObjectFactory factory)
        //    {
        //        ConnectionString = connectionString;
        //        Factory = factory;
        //    }

        //    void IDataMapper.Select(IPersistent entity)
        //    {
        //        throw new NotSupportedException();
        //    }

        //    void IDataMapper.Insert(IPersistent entity)
        //    {
        //        Relation e = (Relation)entity;

        //        bool ok = false;

        //        using (SqlConnection connection = new SqlConnection(ConnectionString))
        //        {
        //            connection.Open();

        //            SqlCommand command  = connection.CreateCommand();
        //            command.CommandType = CommandType.Text;
        //            command.CommandText = InsertCommandText;

        //            SqlParameter parameter = null;

        //            parameter = new SqlParameter("property", SqlDbType.UniqueIdentifier);
        //            parameter.Direction = ParameterDirection.Input;
        //            parameter.Value = e.property.Identity;
        //            command.Parameters.Add(parameter);

        //            parameter = new SqlParameter("entity", SqlDbType.UniqueIdentifier);
        //            parameter.Direction = ParameterDirection.Input;
        //            parameter.Value = e.entity.Identity;
        //            command.Parameters.Add(parameter);

        //            SqlDataReader reader = command.ExecuteReader();

        //            if (reader.Read()) { ok = (int)reader[0] > 0; }

        //            reader.Close(); connection.Close();
        //        }

        //        if (!ok) throw new ApplicationException("Error executing insert command.");
        //    }

        //    void IDataMapper.Update(IPersistent entity)
        //    {
        //        Relation e = (Relation)entity;

        //        bool ok = false;

        //        using (SqlConnection connection = new SqlConnection(ConnectionString))
        //        {
        //            connection.Open();

        //            SqlCommand command  = connection.CreateCommand();
        //            command.CommandType = CommandType.Text;
        //            command.CommandText = UpdateCommandText;

        //            SqlParameter parameter = null;

        //            parameter = new SqlParameter("new_property", SqlDbType.UniqueIdentifier);
        //            parameter.Direction = ParameterDirection.Input;
        //            parameter.Value = e.property.Identity;
        //            command.Parameters.Add(parameter);

        //            parameter = new SqlParameter("new_entity", SqlDbType.UniqueIdentifier);
        //            parameter.Direction = ParameterDirection.Input;
        //            parameter.Value = e.entity.Identity;
        //            command.Parameters.Add(parameter);

        //            parameter = new SqlParameter("old_property", SqlDbType.UniqueIdentifier);
        //            parameter.Direction = ParameterDirection.Input;
        //            parameter.Value = e.old_property.Identity;
        //            command.Parameters.Add(parameter);

        //            parameter = new SqlParameter("old_entity", SqlDbType.UniqueIdentifier);
        //            parameter.Direction = ParameterDirection.Input;
        //            parameter.Value = e.old_entity.Identity;
        //            command.Parameters.Add(parameter);

        //            SqlDataReader reader = command.ExecuteReader();

        //            if (reader.Read()) { ok = (int)reader[0] > 0; }

        //            reader.Close(); connection.Close();
        //        }

        //        if (!ok) throw new ApplicationException("Error executing update command.");
        //    }

        //    void IDataMapper.Delete(IPersistent entity)
        //    {
        //        Relation e = (Relation)entity;

        //        bool ok = false;

        //        using (SqlConnection connection = new SqlConnection(ConnectionString))
        //        {
        //            connection.Open();

        //            SqlCommand command = connection.CreateCommand();
        //            command.CommandType = CommandType.Text;
        //            command.CommandText = DeleteCommandText;

        //            SqlParameter parameter = null;

        //            parameter = new SqlParameter("old_property", SqlDbType.UniqueIdentifier);
        //            parameter.Direction = ParameterDirection.Input;
        //            parameter.Value = e.old_property.Identity;
        //            command.Parameters.Add(parameter);

        //            parameter = new SqlParameter("old_entity", SqlDbType.UniqueIdentifier);
        //            parameter.Direction = ParameterDirection.Input;
        //            parameter.Value = e.old_entity.Identity;
        //            command.Parameters.Add(parameter);

        //            SqlDataReader reader = command.ExecuteReader();

        //            if (reader.Read()) { ok = (int)reader[0] > 0; }

        //            reader.Close(); connection.Close();
        //        }

        //        if (!ok) throw new ApplicationException("Error executing delete command.");
        //    }
        //}
    }
}