using DaJet.Data;
using DaJet.Metadata;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Text;

namespace DaJet.RabbitMQ
{
    public sealed class PgDeliveryTracker : DeliveryTracker
    {
        #region "SQL COMMANDS"

        #region "DATABASE SCHEMA"

        private const string TABLE_EXISTS_COMMAND =
            "SELECT 1 FROM information_schema.tables WHERE table_name = 'delivery_tracking_events';";

        private const string CREATE_TABLE_COMMAND =
            "CREATE TABLE delivery_tracking_events (" +
            "msguid uuid NOT NULL, " +
            "source varchar(10) NOT NULL, " +
            "event_type varchar(16) NOT NULL, " +
            "event_node varchar(10) NOT NULL, " +
            "event_time timestamp NOT NULL, " +
            "event_data text NOT NULL);";

        private const string CREATE_INDEX_COMMAND =
            "CREATE UNIQUE INDEX ix_delivery_tracking_events " +
            "ON delivery_tracking_events USING btree " +
            "(msguid, source, event_type, event_node);";

        private const string CLUSTER_INDEX_COMMAND =
            "ALTER TABLE delivery_tracking_events CLUSTER ON ix_delivery_tracking_events;";

        private const string TYPE_EXISTS_COMMAND =
            "SELECT 1 FROM pg_type WHERE typname = 'delivery_tracking_event';";

        private const string CREATE_TYPE_COMMAND =
            "CREATE TYPE delivery_tracking_event AS (" +
            "msguid uuid, " +
            "source varchar(10), " +
            "event_type varchar(16), " +
            "event_node varchar(10), " +
            "event_time timestamp, " +
            "event_data text);";

        #endregion

        private const string UPSERT_COMMAND =
            "INSERT INTO delivery_tracking_events " +
            "(msguid, source, event_type, event_node, event_time, event_data) " +
            "VALUES " +
            "(@msguid, @source, @event_type, @event_node, @event_time, @event_data) " +
            "ON CONFLICT (msguid, source, event_type, event_node) " +
            "DO UPDATE SET " +
            "event_time = excluded.event_time, " +
            "event_data = excluded.event_data;";

        private const string BULK_UPSERT_COMMAND =
            "INSERT INTO delivery_tracking_events " +
            "(msguid, source, event_type, event_node, event_time, event_data) " +
            "SELECT " +
            "events.msguid, events.source, events.event_type, " +
            "events.event_node, events.event_time, events.event_data " +
            "FROM unnest(@delivery_events) AS events " +
            "ON CONFLICT (msguid, source, event_type, event_node) " +
            "DO UPDATE SET " +
            "event_time = excluded.event_time, " +
            "event_data = excluded.event_data;";

        private const string SELECT_COMMAND =
            "WITH filter AS (SELECT msguid, source, event_type, event_node " +
            "FROM delivery_tracking_events LIMIT 1000) " +
            "DELETE FROM delivery_tracking_events t USING filter " +
            "WHERE t.msguid = filter.msguid " +
            "AND t.source = filter.source " +
            "AND t.event_type = filter.event_type " +
            "AND t.event_node = filter.event_node " +
            "RETURNING " +
            "t.msguid, t.source, t.event_type, t.event_node, t.event_time, t.event_data;";

        //"WITH cte AS (SELECT {МоментВремени}, {Идентификатор} " +
        //"FROM {TABLE_NAME} ORDER BY {МоментВремени} ASC, {Идентификатор} ASC LIMIT @MessageCount), " +
        //"del AS (DELETE FROM {TABLE_NAME} t USING cte " +
        //"WHERE t.{МоментВремени} = cte.{МоментВремени} AND t.{Идентификатор} = cte.{Идентификатор} " +
        //"RETURNING t.{МоментВремени}, t.{Идентификатор}, " +
        //"t.{ДатаВремя}, t.{Отправитель}, " +
        //"t.{Получатели}, t.{ТипОперации}, " +
        //"t.{ТипСообщения}, t.{ТелоСообщения}) " +
        //"SELECT del.{МоментВремени} AS \"МоментВремени\", del.{Идентификатор} AS \"Идентификатор\", " +
        //"del.{ДатаВремя} AS \"ДатаВремя\", CAST(del.{Отправитель} AS varchar) AS \"Отправитель\", " +
        //"CAST(del.{Получатели} AS text) AS \"Получатели\", CAST(del.{ТипОперации} AS varchar) AS \"ТипОперации\", " +
        //"CAST(del.{ТипСообщения} AS varchar) AS \"ТипСообщения\", CAST(del.{ТелоСообщения} AS text) AS \"ТелоСообщения\" " +
        //"FROM del ORDER BY del.{МоментВремени} ASC;";

        #endregion

        private readonly StringBuilder _data = new StringBuilder(1024);
        private readonly QueryExecutor _executor;
        private readonly string _connectionString;
        public PgDeliveryTracker(string connectionString) : base()
        {
            _connectionString = connectionString;
            _executor = new QueryExecutor(DatabaseProvider.PostgreSQL, in _connectionString);
        }
        
        public override void ConfigureDatabase()
        {
            if (_executor.ExecuteScalar<int>(TABLE_EXISTS_COMMAND, 10) != 1)
            {
                List<string> scripts = new List<string>()
                {
                    CREATE_TABLE_COMMAND,
                    CREATE_INDEX_COMMAND,
                    CLUSTER_INDEX_COMMAND
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
            if (_events.Count == 0) { return; }

            List<PgDeliveryEvent> records = new List<PgDeliveryEvent>(_events.Count * 3);

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

            using (NpgsqlConnection connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                connection.TypeMapper.MapComposite<PgDeliveryEvent>("delivery_tracking_event");

                using (NpgsqlTransaction transaction = connection.BeginTransaction())
                {
                    using (NpgsqlCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = BULK_UPSERT_COMMAND;
                        command.Parameters.Add(new NpgsqlParameter()
                        {
                            Value = records,
                            ParameterName = "delivery_events"
                        });
                        
                        _ = command.ExecuteNonQuery();
                    }
                    transaction.Commit();
                }
            }
        }
        private PgDeliveryEvent CreateSelectEvent(in OutMessageInfo deliveryInfo)
        {
            PgDeliveryEvent record = new PgDeliveryEvent()
            {
                MsgUid = deliveryInfo.MsgUid,
                Source = deliveryInfo.AppId,
                EventType = DeliveryEventType.DBRMQ_SELECT,
                EventNode = deliveryInfo.EventNode,
                EventTime = deliveryInfo.EventSelect
            };

            _data.Clear()
                .Append("{\"type\":\"").Append(deliveryInfo.Type)
                .Append("\",\"body\":\"").Append(deliveryInfo.Body)
                .Append("\",\"vector\":\"").Append(deliveryInfo.Vector)
                .Append("\",\"target\":\"").Append(deliveryInfo.Recipients)
                .Append("\"}");

            record.EventData = _data.ToString();

            return record;
        }
        private PgDeliveryEvent CreatePublishEvent(in OutMessageInfo deliveryInfo)
        {
            PgDeliveryEvent record = new PgDeliveryEvent()
            {
                MsgUid = deliveryInfo.MsgUid,
                Source = deliveryInfo.AppId,
                EventType = DeliveryEventType.DBRMQ_PUBLISH,
                EventNode = deliveryInfo.EventNode,
                EventTime = deliveryInfo.EventPublish,
                EventData = string.Empty
            };

            return record;
        }
        private PgDeliveryEvent CreateConfirmEvent(in OutMessageInfo deliveryInfo)
        {
            PgDeliveryEvent record = new PgDeliveryEvent()
            {
                MsgUid = deliveryInfo.MsgUid,
                Source = deliveryInfo.AppId,
                EventNode = deliveryInfo.EventNode,
                EventTime = deliveryInfo.EventConfirm,
                EventData = string.Empty
            };
            
            if (deliveryInfo.EventType == DeliveryEventTypes.DBRMQ_ACK)
            {
                record.EventType = DeliveryEventType.DBRMQ_ACK;
            }
            else if (deliveryInfo.EventType == DeliveryEventTypes.DBRMQ_NACK)
            {
                record.EventType = DeliveryEventType.DBRMQ_NACK;
            }

            return record;
        }
        private PgDeliveryEvent CreateReturnEvent(in OutMessageInfo deliveryInfo)
        {
            PgDeliveryEvent record = new PgDeliveryEvent()
            {
                MsgUid = deliveryInfo.MsgUid,
                Source = deliveryInfo.AppId,
                EventType = DeliveryEventType.DBRMQ_RETURN,
                EventNode = deliveryInfo.EventNode,
                EventTime = deliveryInfo.EventReturn
            };

            ReturnEvent data = new ReturnEvent()
            {
                Reason = deliveryInfo.Body
            };
            record.EventData = data.ToJson();

            return record;
        }

        public override void RegisterEvent(DeliveryEvent @event)
        {
            using (NpgsqlConnection connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                NpgsqlCommand command = connection.CreateCommand();
                NpgsqlTransaction transaction = connection.BeginTransaction();

                command.Connection = connection;
                command.Transaction = transaction;
                command.CommandText = UPSERT_COMMAND;

                command.Parameters.AddWithValue("msguid", @event.MsgUid);
                command.Parameters.AddWithValue("source", @event.Source);
                command.Parameters.AddWithValue("event_type", @event.EventType);
                command.Parameters.AddWithValue("event_node", @event.EventNode);
                command.Parameters.AddWithValue("event_time", @event.EventTime);
                command.Parameters.AddWithValue("event_data", @event.SerializeEventDataToJson());

                try
                {
                    _ = command.ExecuteNonQuery();

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
            
            using (NpgsqlConnection connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                using (NpgsqlTransaction transaction = connection.BeginTransaction())
                {
                    using (NpgsqlCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = SELECT_COMMAND;

                        using (NpgsqlDataReader reader = command.ExecuteReader())
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