using DaJet.Data;
using DaJet.Metadata;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.Server;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace DaJet.RabbitMQ
{
    public sealed class MsDeliveryTracker : DeliveryTracker
    {
        #region "SQL COMMANDS"

        #region "DATABASE SCHEMA"

        private const string TABLE_EXISTS_COMMAND =
            "SELECT 1 FROM sys.tables WHERE name = 'delivery_tracking_events';";

        private const string CREATE_TABLE_COMMAND =
            "CREATE TABLE delivery_tracking_events (" +
            "msguid uniqueidentifier NOT NULL, " +
            "source nvarchar(10) NOT NULL, " +
            "event_type nvarchar(16) NOT NULL, " +
            "event_node nvarchar(10) NOT NULL, " +
            "event_time datetime2 NOT NULL, " +
            "event_data nvarchar(max) NOT NULL);";

        private const string CREATE_INDEX_COMMAND =
            "CREATE UNIQUE CLUSTERED INDEX ix_delivery_tracking_events " +
            "ON delivery_tracking_events (msguid, source, event_type, event_node);";

        private const string TYPE_EXISTS_COMMAND =
            "SELECT 1 FROM sys.types WHERE name = 'delivery_tracking_event';";

        private const string CREATE_TYPE_COMMAND =
            "CREATE TYPE delivery_tracking_event AS TABLE (" +
            "msguid uniqueidentifier NOT NULL, " +
            "source nvarchar(10) NOT NULL, " +
            "event_type nvarchar(16) NOT NULL, " +
            "event_node nvarchar(10) NOT NULL, " +
            "event_time datetime2 NOT NULL, " +
            "event_data nvarchar(max) NOT NULL);";

        #endregion

        #region "INSERT centric UPSERT"

        private const string UPSERT_INSERT_COMMAND =
            "INSERT delivery_tracking_events " +
            "(msguid, source, event_type, event_node, event_time, event_data) " +
            "SELECT " +
            "@msguid, @source, @event_type, @event_node, @event_time, @event_data " +
            "WHERE NOT EXISTS (" +
            "SELECT 1 FROM delivery_tracking_events WITH (UPDLOCK, SERIALIZABLE) " +
            "WHERE msguid = @msguid AND source = @source " +
            "AND event_type = @event_type AND event_node = @event_node" +
            ");";

        private const string UPSERT_UPDATE_COMMAND =
            "UPDATE delivery_tracking_events " +
            "SET event_time = @event_time, event_data = @event_data " +
            "WHERE msguid = @msguid AND source = @source " +
            "AND event_type = @event_type AND event_node = @event_node;";

        #endregion

        #region "UPDATE centric UPSERT"

        private const string UPDATE_COMMAND =
            "UPDATE delivery_tracking_events WITH (UPDLOCK, SERIALIZABLE) " +
            "SET event_time = @event_time, event_data = @event_data " +
            "WHERE msguid = @msguid AND source = @source " +
            "AND event_type = @event_type AND event_node = @event_node;";

        private const string INSERT_COMMAND =
            "INSERT delivery_tracking_events " +
            "(msguid, source, event_type, event_node, event_time, event_data) " +
            "VALUES " +
            "(@msguid, @source, @event_type, @event_node, @event_time, @event_data);";

        #endregion

        #region "JUST DO IT UPSERT"

        private const string UPSERT_COMMAND =
            "BEGIN TRY" +
            "  INSERT delivery_tracking_events " +
            "  (msguid, source, event_type, event_node, event_time, event_data) " +
            "  VALUES " +
            "  (@msguid, @source, @event_type, @event_node, @event_time, @event_data); " +
            "END TRY " +
            "BEGIN CATCH" +
            "  UPDATE delivery_tracking_events " +
            "  SET event_time = @event_time, event_data = @event_data " +
            "  WHERE msguid = @msguid AND source = @source " +
            "  AND event_type = @event_type AND event_node = @event_node;" +
            "END CATCH";

        #endregion

        //private const string BULK_INSERT_COMMAND =
        //    "INSERT delivery_tracking_events " +
        //    "(msguid, source, event_type, event_node, event_time, event_data) " +
        //    "SELECT " +
        //    "msguid, source, event_type, event_node, event_time, event_data " +
        //    "FROM @delivery_events;";

        private const string BULK_UPDATE_COMMAND =
            "UPDATE target WITH (UPDLOCK, SERIALIZABLE) " +
            "SET event_time = events.event_time, event_data = events.event_data " +
            "FROM delivery_tracking_events AS target " +
            "INNER JOIN @delivery_events AS events" +
            " ON target.msguid = events.msguid " +
            "AND target.source = events.source " +
            "AND target.event_type = events.event_type " +
            "AND target.event_node = events.event_node;";

        private const string BULK_INSERT_COMMAND =
            "INSERT delivery_tracking_events " +
            "(msguid, source, event_type, event_node, event_time, event_data) " +
            "SELECT " +
            "msguid, source, event_type, event_node, event_time, event_data " +
            "FROM @delivery_events AS events " +
            "WHERE NOT EXISTS (" +
            "SELECT 1 FROM delivery_tracking_events " +
            "WHERE msguid = events.msguid " +
            "AND source = events.source " +
            "AND event_type = events.event_type " +
            "AND event_node = events.event_node);";

        private const string SELECT_COMMAND =
            "WITH cte AS (SELECT TOP 1000 " +
            "msguid, source, event_type, event_node, event_time, event_data " +
            "FROM delivery_tracking_events WITH (ROWLOCK, READPAST) " +
            "ORDER BY msguid, source, event_type, event_node) " +
            "DELETE cte OUTPUT " +
            "deleted.msguid, deleted.source, deleted.event_type, " +
            "deleted.event_node, deleted.event_time, deleted.event_data;";

        #endregion

        private readonly StringBuilder _data = new StringBuilder(1024);
        private readonly QueryExecutor _executor;
        private readonly string _connectionString;
        private readonly SqlMetaData[] _metadata = new SqlMetaData[]
        {
            new SqlMetaData("msguid", SqlDbType.UniqueIdentifier),
            new SqlMetaData("source", SqlDbType.NVarChar, 10),
            new SqlMetaData("event_type", SqlDbType.NVarChar, 16),
            new SqlMetaData("event_node", SqlDbType.NVarChar, 10),
            new SqlMetaData("event_time", SqlDbType.DateTime2),
            new SqlMetaData("event_data", SqlDbType.NVarChar, -1)
        };
        public MsDeliveryTracker(string connectionString) : base()
        {
            _connectionString = connectionString;
            _executor = new QueryExecutor(DatabaseProvider.SQLServer, in _connectionString);
        }
        
        public override void ConfigureDatabase()
        {
            if (_executor.ExecuteScalar<int>(TABLE_EXISTS_COMMAND, 10) != 1)
            {
                List<string> scripts = new List<string>()
                {
                    CREATE_TABLE_COMMAND,
                    CREATE_INDEX_COMMAND
                };
                _executor.TxExecuteNonQuery(in scripts, 10);
            }

            if (_executor.ExecuteScalar<int>(TYPE_EXISTS_COMMAND, 10) != 1)
            {
                _executor.ExecuteNonQuery(CREATE_TYPE_COMMAND, 10);
            }
        }

        internal override void FlushEvents()
        {
            if (_events.Count == 0)
            {
                return;
            }

            List<SqlDataRecord> records = new List<SqlDataRecord>(_events.Count * 3);

            foreach (OutMessageInfo deliveryInfo in _events.Values)
            {
                if (deliveryInfo.EventSelect != DateTime.MinValue)
                {
                    records.Add(CreateSelectEvent(in deliveryInfo));
                }

                if (deliveryInfo.EventPublish != DateTime.MinValue)
                {
                    records.Add(CreatePublishEvent(in deliveryInfo));
                }

                if (deliveryInfo.EventConfirm != DateTime.MinValue)
                {
                    records.Add(CreateConfirmEvent(in deliveryInfo));
                }

                if (deliveryInfo.EventReturn != DateTime.MinValue)
                {
                    records.Add(CreateReturnEvent(in deliveryInfo));
                }
            }
            _events.Clear();

            int result = 0;

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    using (SqlCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;

                        SqlParameter parameter = command.Parameters.AddWithValue("delivery_events", records);
                        parameter.SqlDbType = SqlDbType.Structured;
                        parameter.TypeName = "delivery_tracking_event";

                        command.CommandText = BULK_UPDATE_COMMAND;
                        result += command.ExecuteNonQuery();

                        command.CommandText = BULK_INSERT_COMMAND;
                        result += command.ExecuteNonQuery();
                    }
                    transaction.Commit();
                }
            }
        }
        private SqlDataRecord CreateSelectEvent(in OutMessageInfo deliveryInfo)
        {
            SqlDataRecord record = new SqlDataRecord(_metadata);

            record.SetGuid(0, deliveryInfo.MsgUid);
            record.SetString(1, deliveryInfo.AppId);
            record.SetString(2, DeliveryEventType.DBRMQ_SELECT);
            record.SetString(3, deliveryInfo.EventNode);
            record.SetDateTime(4, deliveryInfo.EventSelect);
            //record.SetString(5, string.Empty);

            _data.Clear()
                .Append("{\"type\":\"").Append(deliveryInfo.Type)
                .Append("\",\"body\":\"").Append(deliveryInfo.Body)
                .Append("\",\"vector\":\"").Append(deliveryInfo.Vector)
                .Append("\",\"target\":\"").Append(deliveryInfo.Recipients)
                .Append("\"}");

            record.SetString(5, _data.ToString());

            return record;
        }
        private SqlDataRecord CreatePublishEvent(in OutMessageInfo deliveryInfo)
        {
            SqlDataRecord record = new SqlDataRecord(_metadata);

            record.SetGuid(0, deliveryInfo.MsgUid);
            record.SetString(1, deliveryInfo.AppId);
            record.SetString(2, DeliveryEventType.DBRMQ_PUBLISH);
            record.SetString(3, deliveryInfo.EventNode);
            record.SetDateTime(4, deliveryInfo.EventPublish);
            record.SetString(5, string.Empty);

            return record;
        }
        private SqlDataRecord CreateConfirmEvent(in OutMessageInfo deliveryInfo)
        {
            SqlDataRecord record = new SqlDataRecord(_metadata);

            record.SetGuid(0, deliveryInfo.MsgUid);
            record.SetString(1, deliveryInfo.AppId);
            if (deliveryInfo.EventType == DeliveryEventTypes.DBRMQ_ACK)
            {
                record.SetString(2, DeliveryEventType.DBRMQ_ACK);
            }
            else if (deliveryInfo.EventType == DeliveryEventTypes.DBRMQ_NACK)
            {
                record.SetString(2, DeliveryEventType.DBRMQ_NACK);
            }
            record.SetString(3, deliveryInfo.EventNode);
            record.SetDateTime(4, deliveryInfo.EventConfirm);
            record.SetString(5, string.Empty);

            return record;
        }
        private SqlDataRecord CreateReturnEvent(in OutMessageInfo deliveryInfo)
        {
            SqlDataRecord record = new SqlDataRecord(_metadata);

            record.SetGuid(0, deliveryInfo.MsgUid);
            record.SetString(1, deliveryInfo.AppId);
            record.SetString(2, DeliveryEventType.DBRMQ_RETURN);
            record.SetString(3, deliveryInfo.EventNode);
            record.SetDateTime(4, deliveryInfo.EventReturn);

            ReturnEvent data = new ReturnEvent()
            {
                Reason = deliveryInfo.Body
            };
            record.SetString(5, data.ToJson());

            return record;
        }

        public override void RegisterEvent(DeliveryEvent @event)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                SqlCommand command = connection.CreateCommand();
                SqlTransaction transaction = connection.BeginTransaction();

                command.Connection = connection;
                command.Transaction = transaction;

                command.Parameters.AddWithValue("msguid", @event.MsgUid);
                command.Parameters.AddWithValue("source", @event.Source);
                command.Parameters.AddWithValue("event_type", @event.EventType);
                command.Parameters.AddWithValue("event_node", @event.EventNode);
                command.Parameters.AddWithValue("event_time", @event.EventTime);
                command.Parameters.AddWithValue("event_data", @event.SerializeEventDataToJson());

                try
                {
                    command.CommandText = UPSERT_INSERT_COMMAND;
                    int recordsAffected = command.ExecuteNonQuery();
                    if (recordsAffected == 0)
                    {
                        command.CommandText = UPSERT_UPDATE_COMMAND;
                        _ = command.ExecuteNonQuery();
                    }
                    transaction.Commit();
                }
                catch (Exception error)
                {
                    try
                    {
                        transaction.Rollback();
                    }
                    catch
                    {
                        // do nothing
                    }
                    throw error;
                }
            }
        }

        public override int ProcessEvents(IDeliveryEventProcessor processor)
        {
            DeliveryEvent @event = new DeliveryEvent();

            int consumed = 0;
            
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    using (SqlCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = SELECT_COMMAND;

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                consumed++;

                                @event.MsgUid = reader.GetGuid(0);
                                @event.Source = reader.GetString(1);
                                @event.EventType = reader.GetString(2);
                                @event.EventNode = reader.GetString(3);
                                @event.EventTime = reader.GetDateTime(4);
                                @event.EventData = reader.GetString(5);

                                processor.Process(@event);
                            }
                            reader.Close();
                        }
                    }
                    processor.Synchronize();
                    
                    transaction.Commit();
                }
            }

            return consumed;
        }
    }
}