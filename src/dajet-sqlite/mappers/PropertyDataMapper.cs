using DaJet.Data;
using Microsoft.Data.Sqlite;
using System;
using System.Collections;
using System.Collections.Generic;

namespace DaJet.Sqlite
{
    public sealed class PropertyDataMapper : IDataMapper
    {
        private readonly int MY_TYPE_CODE;
        private readonly int ENTITY_TYPE_CODE;
        private readonly IDataSource _source;
        #region "SQL"
        private const string SELECT_BY_UUID =
            "SELECT uuid, owner, name, readonly, column_name, type, length, fixed, precision, scale, signed, " +
            "discriminator, primary_key FROM dajet_property WHERE uuid = @uuid;";
        private const string SELECT_BY_OWNER =
            "SELECT uuid, owner, name, readonly, column_name, type, length, fixed, precision, scale, signed, " +
            "discriminator, primary_key FROM dajet_property WHERE owner = @owner;";
        private const string INSERT_COMMAND =
            "INSERT INTO dajet_property " +
            "(uuid, owner, name, readonly, column_name, type, length, fixed, precision, scale, signed, discriminator, primary_key) " +
            "VALUES " +
            "(@uuid, @owner, @name, @readonly, @column_name, @type, @length, @fixed, @precision, @scale, @signed, @discriminator, @primary_key);";
        private const string UPDATE_COMMAND =
            "UPDATE dajet_property SET owner = @owner, name = @name, readonly = @readonly, column_name = @column_name, " +
            "type = @type, length = @length, fixed = @fixed, precision = @precision, scale = @scale, signed = @signed, " +
            "discriminator = @discriminator, primary_key = @primary_key WHERE uuid = @uuid;";
        private const string DELETE_COMMAND =
            "DELETE FROM dajet_property WHERE uuid = @uuid;";
        #endregion
        public PropertyDataMapper(IDataSource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));

            MY_TYPE_CODE = _source.Model.GetTypeCode(typeof(PropertyRecord));

            ENTITY_TYPE_CODE = _source.Model.GetTypeCode(typeof(EntityRecord));
        }
        public IEnumerable Select() { throw new NotImplementedException(); }
        public EntityObject Select(int code) { throw new NotImplementedException(); }
        public EntityObject Select(string name) { throw new NotImplementedException(); }
        public IEnumerable Select(Entity owner)
        {
            List<PropertyRecord> list = new();

            if (owner.TypeCode != ENTITY_TYPE_CODE)
            {
                return list;
            }

            using (SqliteConnection connection = new(_source.ConnectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SELECT_BY_OWNER;

                    command.Parameters.AddWithValue("owner", owner.Identity.ToString().ToLowerInvariant());

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            PropertyRecord item = new()
                            {
                                TypeCode = MY_TYPE_CODE,
                                Identity = new Guid(reader.GetString(0)),
                                Owner = new Entity(ENTITY_TYPE_CODE, new(reader.GetString(1))),
                                Name = reader.GetString(2),
                                IsReadOnly = reader.GetInt64(3) == 1L,
                                Column = reader.GetString(4),
                                Type = reader.GetString(5),
                                Length = (int)reader.GetInt64(6),
                                IsFixed = reader.GetInt64(7) == 1L,
                                Precision = (int)reader.GetInt64(8),
                                Scale = (int)reader.GetInt64(9),
                                IsSigned = reader.GetInt64(10) == 1L,
                                Discriminator = (int)reader.GetInt64(11),
                                PrimaryKey = (int)reader.GetInt64(12)
                            };
                            item.MarkAsOriginal();

                            list.Add(item);
                        }
                        reader.Close();
                    }
                }
            }

            return list;
        }
        public EntityObject Select(Guid idenity)
        {
            PropertyRecord record = null;

            using (SqliteConnection connection = new(_source.ConnectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = SELECT_BY_UUID;

                    command.Parameters.AddWithValue("uuid", idenity.ToString().ToLowerInvariant());

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            record = new PropertyRecord()
                            {
                                TypeCode = MY_TYPE_CODE,
                                Identity = new Guid(reader.GetString(0)),
                                Owner = new Entity(ENTITY_TYPE_CODE, new(reader.GetString(1))),
                                Name = reader.GetString(2),
                                IsReadOnly = reader.GetInt64(3) == 1L,
                                Column = reader.GetString(4),
                                Type = reader.GetString(5),
                                Length = (int)reader.GetInt64(6),
                                IsFixed = reader.GetInt64(7) == 1L,
                                Precision = (int)reader.GetInt64(8),
                                Scale = (int)reader.GetInt64(9),
                                IsSigned = reader.GetInt64(10) == 1L,
                                Discriminator = (int)reader.GetInt64(11),
                                PrimaryKey = (int)reader.GetInt64(12)
                            };
                            record.MarkAsOriginal();
                        }
                        reader.Close();
                    }
                }
            }

            return record;
        }
        public void Insert(EntityObject entity)
        {
            if (entity is not PropertyRecord record)
            {
                return;
            }

            using (SqliteConnection connection = new(_source.ConnectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = INSERT_COMMAND;

                    command.Parameters.AddWithValue("uuid", record.Identity.ToString().ToLowerInvariant());
                    command.Parameters.AddWithValue("owner", record.Owner.Identity.ToString().ToLowerInvariant());
                    command.Parameters.AddWithValue("name", record.Name);
                    command.Parameters.AddWithValue("readonly", record.IsReadOnly ? 1L : 0L);
                    command.Parameters.AddWithValue("column_name", record.Column);
                    command.Parameters.AddWithValue("type", record.Type);
                    command.Parameters.AddWithValue("length", record.Length);
                    command.Parameters.AddWithValue("fixed", record.IsFixed ? 1L : 0L);
                    command.Parameters.AddWithValue("precision", record.Precision);
                    command.Parameters.AddWithValue("scale", record.Scale);
                    command.Parameters.AddWithValue("signed", record.IsSigned ? 1L : 0L);
                    command.Parameters.AddWithValue("discriminator", record.Discriminator);
                    command.Parameters.AddWithValue("primary_key", record.PrimaryKey);

                    int result = command.ExecuteNonQuery();
                }
            }
        }
        public void Update(EntityObject entity)
        {
            if (entity is not PropertyRecord record)
            {
                return;
            }

            using (SqliteConnection connection = new(_source.ConnectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = UPDATE_COMMAND;

                    command.Parameters.AddWithValue("uuid", record.Identity.ToString().ToLowerInvariant());
                    command.Parameters.AddWithValue("owner", record.Owner.Identity.ToString().ToLowerInvariant());
                    command.Parameters.AddWithValue("name", record.Name);
                    command.Parameters.AddWithValue("readonly", record.IsReadOnly ? 1L : 0L);
                    command.Parameters.AddWithValue("column_name", record.Column);
                    command.Parameters.AddWithValue("type", record.Type);
                    command.Parameters.AddWithValue("length", record.Length);
                    command.Parameters.AddWithValue("fixed", record.IsFixed ? 1L : 0L);
                    command.Parameters.AddWithValue("precision", record.Precision);
                    command.Parameters.AddWithValue("scale", record.Scale);
                    command.Parameters.AddWithValue("signed", record.IsSigned ? 1L : 0L);
                    command.Parameters.AddWithValue("discriminator", record.Discriminator);
                    command.Parameters.AddWithValue("primary_key", record.PrimaryKey);

                    int result = command.ExecuteNonQuery();
                }
            }
        }
        public void Delete(Entity entity)
        {
            using (SqliteConnection connection = new(_source.ConnectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = DELETE_COMMAND;

                    command.Parameters.AddWithValue("uuid", entity.Identity.ToString().ToLowerInvariant());

                    int result = command.ExecuteNonQuery();
                }
            }
        }
    }
}