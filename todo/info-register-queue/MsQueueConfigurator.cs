using DaJet.Metadata;
using DaJet.Metadata.Model;
using System;
using System.Collections.Generic;

namespace DaJet.Data.Messaging.V1
{
    public sealed class MsQueueConfigurator : IQueueConfigurator
    {
        private readonly QueryBuilder _builder;
        private readonly QueryExecutor _executor;
        public MsQueueConfigurator(in string connectionString)
        {
            _builder = new QueryBuilder(DatabaseProvider.SQLServer);
            _executor = new QueryExecutor(DatabaseProvider.SQLServer, in connectionString);
        }
        
        #region "CONFIGURE SEQUENCE"

        private const string SEQUENCE_EXISTS_SCRIPT = "SELECT 1 FROM sys.sequences WHERE name = '{SEQUENCE_NAME}';";

        private const string CREATE_SEQUENCE_SCRIPT =
            "IF NOT EXISTS(SELECT 1 FROM sys.sequences WHERE name = '{SEQUENCE_NAME}') " +
            "BEGIN CREATE SEQUENCE {SEQUENCE_NAME} AS numeric(19,0) START WITH 1 INCREMENT BY 1; END;";

        private bool SequenceExists(in ApplicationObject queue)
        {
            List<string> templates = new List<string>()
            {
                SEQUENCE_EXISTS_SCRIPT
            };
            
            _builder.ConfigureScripts(in templates, in queue, out List<string> scripts);

            int result = _executor.ExecuteScalar<int>(scripts[0], 10);

            return (result == 1);
        }

        #endregion

        #region "ENUMERATE QUEUE SCRIPTS"

        private const string ENUMERATE_INCOMING_QUEUE_SCRIPT =
            "SELECT {НомерСообщения} AS [НомерСообщения], " +
            "NEXT VALUE FOR {SEQUENCE_NAME} OVER(ORDER BY {НомерСообщения} ASC) AS [Порядок] " +
            "INTO #{TABLE_NAME}_EnumCopy " +
            "FROM {TABLE_NAME} WITH (TABLOCKX, HOLDLOCK); " +
            "UPDATE T SET T.{НомерСообщения} = C.[Порядок] FROM {TABLE_NAME} AS T " +
            "INNER JOIN #{TABLE_NAME}_EnumCopy AS C ON T.{НомерСообщения} = C.[НомерСообщения];";

        private const string ENUMERATE_OUTGOING_QUEUE_SCRIPT =
            "SELECT {НомерСообщения} AS [НомерСообщения], {Идентификатор} AS [Идентификатор], " +
            "NEXT VALUE FOR {SEQUENCE_NAME} OVER(ORDER BY {НомерСообщения} ASC, {Идентификатор} ASC) AS [Порядок] " +
            "INTO #{TABLE_NAME}_EnumCopy " +
            "FROM {TABLE_NAME} WITH (TABLOCKX, HOLDLOCK); " +
            "UPDATE T SET T.{НомерСообщения} = C.[Порядок] FROM {TABLE_NAME} AS T " +
            "INNER JOIN #{TABLE_NAME}_EnumCopy AS C ON T.{НомерСообщения} = C.[НомерСообщения] AND T.{Идентификатор} = C.[Идентификатор];";

        private const string DROP_ENUMERATION_TABLE = "DROP TABLE #{TABLE_NAME}_EnumCopy;";

        #endregion

        #region "CONFIGURE INCOMING QUEUE"

        public void ConfigureIncomingMessageQueue(in ApplicationObject queue, out List<string> errors)
        {
            errors = new List<string>();

            try
            {
                if (!SequenceExists(in queue))
                {
                    ConfigureIncomingQueue(in queue);
                }
            }
            catch (Exception error)
            {
                errors.Add(ExceptionHelper.GetErrorText(error));
            }
        }
        private void ConfigureIncomingQueue(in ApplicationObject queue)
        {
            List<string> templates = new List<string>()
            {
                CREATE_SEQUENCE_SCRIPT,
                ENUMERATE_INCOMING_QUEUE_SCRIPT,
                DROP_ENUMERATION_TABLE
            };

            _builder.ConfigureScripts(in templates, in queue, out List<string> scripts);

            _executor.TxExecuteNonQuery(in scripts, 60);
        }

        #endregion

        #region "CONFIGURE OUTGOING QUEUE"

        private const string OUTGOING_TRIGGER_EXISTS =
            "SELECT 1 FROM sys.triggers WHERE name = '{TRIGGER_NAME}';";

        private const string DROP_OUTGOING_TRIGGER_SCRIPT =
            "IF OBJECT_ID('{TRIGGER_NAME}', 'TR') IS NOT NULL BEGIN DROP TRIGGER {TRIGGER_NAME} END;";

        private const string CREATE_OUTGOING_TRIGGER_SCRIPT =
            "CREATE TRIGGER {TRIGGER_NAME} ON {TABLE_NAME} INSTEAD OF INSERT NOT FOR REPLICATION AS " +
            "INSERT {TABLE_NAME} " +
            "({НомерСообщения}, {Идентификатор}, {Заголовки}, {ТипСообщения}, {ТелоСообщения}, {Ссылка}, {ДатаВремя}) " +
            "SELECT NEXT VALUE FOR {SEQUENCE_NAME}, " +
            "i.{Идентификатор}, i.{Заголовки}, i.{ТипСообщения}, i.{ТелоСообщения}, i.{Ссылка}, i.{ДатаВремя} " +
            "FROM inserted AS i;";

        private const string ENABLE_OUTGOING_TRIGGER_SCRIPT = "ENABLE TRIGGER {TRIGGER_NAME} ON {TABLE_NAME};";

        private bool OutgoingTriggerExists(in ApplicationObject queue)
        {
            List<string> templates = new List<string>()
            {
                OUTGOING_TRIGGER_EXISTS
            };

            _builder.ConfigureScripts(in templates, in queue, out List<string> scripts);

            int result = _executor.ExecuteScalar<int>(scripts[0], 10);

            return (result == 1);
        }

        public void ConfigureOutgoingMessageQueue(in ApplicationObject queue, out List<string> errors)
        {
            errors = new List<string>();

            try
            {
                if (!SequenceExists(in queue) || !OutgoingTriggerExists(in queue))
                {
                    ConfigureOutgoingQueue(in queue);
                }
            }
            catch (Exception error)
            {
                errors.Add(ExceptionHelper.GetErrorText(error));
            }
        }
        private void ConfigureOutgoingQueue(in ApplicationObject queue)
        {
            List<string> templates = new List<string>()
            {
                CREATE_SEQUENCE_SCRIPT,
                ENUMERATE_OUTGOING_QUEUE_SCRIPT,
                DROP_ENUMERATION_TABLE,
                DROP_OUTGOING_TRIGGER_SCRIPT,
                CREATE_OUTGOING_TRIGGER_SCRIPT,
                ENABLE_OUTGOING_TRIGGER_SCRIPT
            };

            _builder.ConfigureScripts(in templates, in queue, out List<string> scripts);

            _executor.TxExecuteNonQuery(in scripts, 60);
        }

        #endregion
    }
}