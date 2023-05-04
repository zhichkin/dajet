using DaJet.Metadata;
using DaJet.Metadata.Model;
using System;
using System.Collections.Generic;

namespace DaJet.Data.Messaging.V1
{
    public sealed class PgQueueConfigurator : IQueueConfigurator
    {
        private readonly QueryBuilder _builder;
        private readonly QueryExecutor _executor;
        public PgQueueConfigurator(in string connectionString)
        {
            _builder = new QueryBuilder(DatabaseProvider.PostgreSQL);
            _executor = new QueryExecutor(DatabaseProvider.PostgreSQL, in connectionString);
        }

        #region "CONFIGURE SEQUENCE"

        private const string SEQUENCE_EXISTS_SCRIPT =
            "SELECT 1 FROM information_schema.sequences WHERE LOWER(sequence_name) = LOWER('{SEQUENCE_NAME}');";

        private const string CREATE_SEQUENCE_SCRIPT =
            "CREATE SEQUENCE IF NOT EXISTS {SEQUENCE_NAME} AS bigint INCREMENT BY 1 START WITH 1 CACHE 1;";

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
            "LOCK TABLE {TABLE_NAME} IN ACCESS EXCLUSIVE MODE; " +
            "WITH cte AS (SELECT {НомерСообщения}, nextval('{SEQUENCE_NAME}') AS msgno " +
            "FROM {TABLE_NAME} ORDER BY {НомерСообщения} ASC) " +
            "UPDATE {TABLE_NAME} SET {НомерСообщения} = CAST(cte.msgno AS numeric(19, 0)) " +
            "FROM cte WHERE {TABLE_NAME}.{НомерСообщения} = cte.{НомерСообщения};";

        private const string ENUMERATE_OUTGOING_QUEUE_SCRIPT =
            "LOCK TABLE {TABLE_NAME} IN ACCESS EXCLUSIVE MODE; " +
            "WITH cte AS (SELECT {НомерСообщения}, {Идентификатор}, nextval('{SEQUENCE_NAME}') AS msgno " +
            "FROM {TABLE_NAME} ORDER BY {НомерСообщения} ASC, {Идентификатор} ASC) " +
            "UPDATE {TABLE_NAME} SET {НомерСообщения} = CAST(cte.msgno AS numeric(19, 0)) " +
            "FROM cte WHERE {TABLE_NAME}.{НомерСообщения} = cte.{НомерСообщения} AND {TABLE_NAME}.{Идентификатор} = cte.{Идентификатор};";

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
            List<string> templates = new List<string>
            {
                CREATE_SEQUENCE_SCRIPT,
                ENUMERATE_INCOMING_QUEUE_SCRIPT
            };

            _builder.ConfigureScripts(in templates, in queue, out List<string> scripts);

            _executor.TxExecuteNonQuery(in scripts, 60);
        }

        #endregion

        #region "CONFIGURE OUTGOING QUEUE"

        private const string OUTGOING_TRIGGER_EXISTS =
            "SELECT 1 FROM information_schema.triggers WHERE LOWER(trigger_name) = LOWER('{TRIGGER_NAME}');";

        private const string CREATE_OUTGOING_FUNCTION_SCRIPT =
            "CREATE OR REPLACE FUNCTION {FUNCTION_NAME} RETURNS trigger AS $$ BEGIN " +
            "NEW.{НомерСообщения} := CAST(nextval('{SEQUENCE_NAME}') AS numeric(19,0)); RETURN NEW; END $$ LANGUAGE 'plpgsql';";

        private const string DROP_OUTGOING_TRIGGER_SCRIPT = "DROP TRIGGER IF EXISTS {TRIGGER_NAME} ON {TABLE_NAME};";

        private const string CREATE_OUTGOING_TRIGGER_SCRIPT =
            "CREATE TRIGGER {TRIGGER_NAME} BEFORE INSERT ON {TABLE_NAME} FOR EACH ROW EXECUTE PROCEDURE {FUNCTION_NAME};";

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
            List<string> templates = new List<string>
            {
                CREATE_SEQUENCE_SCRIPT,
                ENUMERATE_OUTGOING_QUEUE_SCRIPT,
                CREATE_OUTGOING_FUNCTION_SCRIPT,
                DROP_OUTGOING_TRIGGER_SCRIPT,
                CREATE_OUTGOING_TRIGGER_SCRIPT
            };

            _builder.ConfigureScripts(in templates, in queue, out List<string> scripts);

            _executor.TxExecuteNonQuery(in scripts, 60);
        }

        #endregion
    }
}