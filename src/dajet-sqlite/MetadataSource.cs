using DaJet.Data;
using DaJet.Model;
using Microsoft.Data.Sqlite;
using System;
using System.Collections;
using System.Collections.Generic;

namespace DaJet.Sqlite
{
    public sealed class MetadataSource : IDataSource
    {
        private readonly IDomainModel _domain;
        private readonly string _connectionString;
        private readonly Dictionary<Type, IDataMapper> _mappers;
        public MetadataSource(in string databaseFileFullPath)
        {
            _connectionString = new SqliteConnectionStringBuilder()
            {
                DataSource = databaseFileFullPath,
                Mode = SqliteOpenMode.ReadWriteCreate
            }
            .ToString();

            _domain = new DomainModel(3);
            _domain.Entity<NamespaceRecord>(1);
            _domain.Entity<EntityRecord>(2);
            _domain.Entity<PropertyRecord>(3);

            _mappers = new Dictionary<Type, IDataMapper>(3)
            {
                { typeof(NamespaceRecord), new NamespaceDataMapper(this) },
                { typeof(EntityRecord), new EntityDataMapper(this) },
                { typeof(PropertyRecord), new PropertyDataMapper(this) }
            };
        }
        public IDomainModel Model { get { return _domain; } }
        public string ConnectionString { get { return _connectionString; } }

        public void Create(EntityObject entity)
        {
            if (_mappers.TryGetValue(entity.GetType(), out IDataMapper mapper))
            {
                mapper.Insert(entity);
            }
        }
        public void Update(EntityObject entity)
        {
            if (_mappers.TryGetValue(entity.GetType(), out IDataMapper mapper))
            {
                mapper.Update(entity);
            }
        }
        public void Delete(Entity entity)
        {
            Type type = _domain.GetEntityType(entity.TypeCode);

            if (_mappers.TryGetValue(type, out IDataMapper mapper))
            {
                mapper.Delete(entity);
            }
        }
        public void Delete<T>(Guid identity) where T : EntityObject
        {
            Entity entity = _domain.GetEntity<T>(identity);

            if (_mappers.TryGetValue(typeof(T), out IDataMapper mapper))
            {
                mapper.Delete(entity);
            }
        }

        public EntityObject Select(Entity entity)
        {
            if (entity.IsUndefined || entity.IsEmpty)
            {
                return null;
            }

            Type type = _domain.GetEntityType(entity.TypeCode);

            if (_mappers.TryGetValue(type, out IDataMapper mapper))
            {
                return mapper.Select(entity.Identity);
            }

            return null;
        }
        public IEnumerable Select(int typeCode)
        {
            Type type = _domain.GetEntityType(typeCode);

            if (_mappers.TryGetValue(type, out IDataMapper mapper))
            {
                return mapper.Select();
            }

            return null;
        }
        public IEnumerable Select(int typeCode, Entity owner)
        {
            Type type = _domain.GetEntityType(typeCode);

            if (_mappers.TryGetValue(type, out IDataMapper mapper))
            {
                return mapper.Select(owner);
            }

            return null;
        }

        public T Select<T>(int code) where T : EntityObject
        {
            if (_mappers.TryGetValue(typeof(T), out IDataMapper mapper))
            {
                return mapper.Select(code) as T;
            }

            return null;
        }
        public T Select<T>(string name) where T : EntityObject
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            if (_mappers.TryGetValue(typeof(T), out IDataMapper mapper))
            {
                return mapper.Select(name) as T;
            }

            return null;
        }
        public T Select<T>(Guid identity) where T : EntityObject
        {
            if (_mappers.TryGetValue(typeof(T), out IDataMapper mapper))
            {
                return mapper.Select(identity) as T;
            }
            return null;
        }
        public T Select<T>(Entity entity) where T : EntityObject
        {
            if (entity.IsUndefined || entity.IsEmpty)
            {
                return null;
            }
            return Select<T>(entity.Identity);
        }
        public IEnumerable<T> Query<T>() where T : EntityObject
        {
            if (_mappers.TryGetValue(typeof(T), out IDataMapper mapper))
            {
                return mapper.Select() as IEnumerable<T>;
            }
            return null;
        }
        public IEnumerable<T> Query<T>(Entity owner) where T : EntityObject
        {
            if (_mappers.TryGetValue(typeof(T), out IDataMapper mapper))
            {
                return mapper.Select(owner) as IEnumerable<T>;
            }
            return null;
        }
        public List<DataObject> Query(string query, Dictionary<string, object> parameters)
        {
            List<DataObject> list = new();

            using (SqliteConnection connection = new(ConnectionString))
            {
                connection.Open();

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = query;

                    foreach (var parameter in parameters)
                    {
                        if (parameter.Value is Guid uuid)
                        {
                            command.Parameters.AddWithValue(parameter.Key, uuid.ToString().ToLower());
                        }
                        else
                        {
                            command.Parameters.AddWithValue(parameter.Key, parameter.Value);
                        }
                    }

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            DataObject record = new(reader.FieldCount);

                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                record.SetValue(reader.GetName(i), reader.GetValue(i));
                            }

                            list.Add(record);
                        }
                        reader.Close();
                    }
                }
            }

            return list;
        }
    }
}

//CREATE TABLE sqlite_schema (type text, name text, tbl_name text, rootpage integer, sql text);
//private const string TABLE_EXISTS_COMMAND = "SELECT 1 FROM sqlite_schema WHERE name = @table_name;";
//private const string SELECT_TABLE_COLUMNS =
//"SELECT c.name, c.type, c.pk, c.notnull " +
//"FROM sqlite_schema AS s " +
//"INNER JOIN pragma_table_info(s.name) AS c " +
//"WHERE s.type = 'table' AND s.name = @table_name " +
//"ORDER BY c.cid ASC;";