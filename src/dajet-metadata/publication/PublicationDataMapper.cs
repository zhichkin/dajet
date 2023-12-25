using DaJet.Data;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using System;
using System.Data;

namespace DaJet.Metadata.Services
{
    internal class PublicationDataMapper
    {
        private const string MS_SELECT_SUBSCRIBERS_QUERY_TEMPLATE =
            "SELECT _IDRRef, _Code, _Description, CAST(_Marked AS bit), _PredefinedID FROM {0};";
        private const string PG_SELECT_SUBSCRIBERS_QUERY_TEMPLATE =
            "SELECT _idrref, CAST(_code AS varchar), CAST(_description AS varchar), _marked, _predefinedid FROM {0};";

        private readonly object _lock = new();
        private readonly OneDbMetadataProvider _cache;
        internal PublicationDataMapper(OneDbMetadataProvider cache)
        {
            _cache = cache;
        }
        private string CreateSelectSubscribersScript(Publication publication)
        {
            if (_cache.DatabaseProvider == DatabaseProvider.SqlServer)
            {
                return string.Format(
                    MS_SELECT_SUBSCRIBERS_QUERY_TEMPLATE,
                    publication.TableName);
            }
            else if (_cache.DatabaseProvider == DatabaseProvider.PostgreSql)
            {
                return string.Format(
                    PG_SELECT_SUBSCRIBERS_QUERY_TEMPLATE,
                    publication.TableName.ToLowerInvariant());
            }

            throw new InvalidOperationException($"Unsupported database provider: {_cache.DatabaseProvider}");
        }
        internal void Select(in Publication publication)
        {
            lock (_lock)
            {
                SelectSynchronized(in publication);
            }
        }
        private void SelectSynchronized(in Publication publication)
        {
            publication.Publisher = null;
            publication.Subscribers.Clear();

            IQueryExecutor executor = _cache.CreateQueryExecutor();

            string script = CreateSelectSubscribersScript(publication);

            foreach (IDataReader reader in executor.ExecuteReader(script, 10))
            {
                Guid predefinedid = new Guid((byte[])reader[4]);

                if (predefinedid == Guid.Empty)
                {
                    Subscriber subscriber = new()
                    {
                        Uuid = new Guid((byte[])reader[0]),
                        Code = reader.GetString(1),
                        Name = reader.GetString(2),
                        IsMarkedForDeletion = reader.GetBoolean(3)
                    };

                    publication.Subscribers.Add(subscriber);
                }
                else
                {
                    Publisher publisher = new()
                    {
                        Uuid = new Guid((byte[])reader[0]),
                        Code = reader.GetString(1),
                        Name = reader.GetString(2)
                    };

                    publication.Publisher = publisher;
                }
            }
        }
    }
}