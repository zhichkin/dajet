using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.Text;

namespace DaJet.Pipeline.Test
{
    [TestClass] public class Test_Pipeline
    {
        private const string MS_CONNECTION_STRING = "Data Source=ZHICHKIN;Initial Catalog=dajet-messaging-ms;Integrated Security=True;Encrypt=False;";
        private const string PG_CONNECTION_STRING = "Host=127.0.0.1;Port=5432;Database=dajet-messaging-pg;Username=postgres;Password=postgres;";
        private string GetSourceScript_SqlServer()
        {
            StringBuilder script = new();

            script.Append("WITH cte AS (SELECT TOP (@MessagesPerTransaction) _Fld261 AS МоментВремени, ");
            script.Append("_Fld262 AS Идентификатор, _Fld263 AS Заголовки, _Fld264 AS Отправитель, _Fld265 AS Получатели, ");
            script.Append("_Fld266 AS ТипСообщения, _Fld267 AS ТелоСообщения, _Fld269 AS ДатаВремя, _Fld338 AS ТипОперации ");
            script.Append("FROM _InfoRg260 WITH (ROWLOCK, READPAST) ORDER BY _Fld261 ASC, _Fld262 ASC) DELETE cte OUTPUT ");
            script.Append("deleted.МоментВремени, deleted.Идентификатор, deleted.Заголовки, deleted.Отправитель, deleted.Получатели, ");
            script.Append("deleted.ТипСообщения, deleted.ТелоСообщения, deleted.ДатаВремя, deleted.ТипОперации;");

            return script.ToString();
        }
        private string GetTargetScript_SqlServer()
        {
            StringBuilder script = new();

            script.Append("INSERT _InfoRg250 ");
            script.Append("(_Fld251, _Fld252, _Fld253, _Fld254, _Fld255, _Fld256,");
            script.Append(" _Fld257, _Fld337, _Fld258, _Fld259) ");
            script.Append("SELECT NEXT VALUE FOR DaJetIncomingQueueSequence, ");
            script.Append("@Идентификатор, @Заголовки, @Отправитель, @ТипСообщения, @ТелоСообщения,");
            script.Append("@ДатаВремя, @ТипОперации, @ОписаниеОшибки, @КоличествоОшибок;");

            return script.ToString();
        }
        private string GetSourceScript_PostgreSql()
        {
            StringBuilder script = new();

            script.Append("WITH cte AS (SELECT _Fld141, _Fld142 FROM _InfoRg140 ");
            script.Append("ORDER BY _Fld141 ASC, _Fld142 ASC LIMIT @MessagesPerTransaction), ");
            script.Append("del AS (DELETE FROM _InfoRg140 t USING cte ");
            script.Append("WHERE t._Fld141 = cte._Fld141 AND t._Fld142 = cte._Fld142 ");
            script.Append("RETURNING t._Fld141, t._Fld142, ");
            script.Append("t._Fld143, t._Fld144, t._Fld145, ");
            script.Append("t._Fld146, t._Fld147, ");
            script.Append("t._Fld148, t._Fld149, t._Fld150, t._Fld151) ");
            script.Append("SELECT del._Fld141 AS \"МоментВремени\", del._Fld142 AS \"Идентификатор\", ");
            script.Append("CAST(del._Fld143 AS text) AS \"Заголовки\", ");
            script.Append("CAST(del._Fld144 AS varchar) AS \"Отправитель\", CAST(del._Fld145 AS text) AS \"Получатели\", ");
            script.Append("CAST(del._Fld146 AS varchar) AS \"ТипСообщения\", CAST(del._Fld147 AS text) AS \"ТелоСообщения\", ");
            script.Append("del._Fld148 AS \"ДатаВремя\", CAST(del._Fld149 AS varchar) AS \"ТипОперации\", ");
            script.Append("CAST(del._Fld150 AS varchar) AS \"ОписаниеОшибки\", del._Fld151 AS \"КоличествоОшибок\" ");
            script.Append("FROM del ORDER BY del._Fld141 ASC;");

            return script.ToString();
        }
        private string GetTargetScript_PostgreSql()
        {
            StringBuilder script = new();

            script.Append("INSERT INTO _InfoRg91 ");
            script.Append("(_Fld92, _Fld93, _Fld94, _Fld95, _Fld96, _Fld97, _Fld98, _Fld99, _Fld100, _Fld101) ");
            script.Append("SELECT CAST(nextval('DaJetIncomingQueueSequence') AS numeric(19,0)), @Идентификатор, ");
            script.Append("CAST(@Заголовки AS mvarchar), CAST(@Отправитель AS mvarchar), ");
            script.Append("CAST(@ТипСообщения AS mvarchar), CAST(@ТелоСообщения AS mvarchar), ");
            script.Append("@ДатаВремя, CAST(@ТипОперации AS mvarchar), CAST(@ОписаниеОшибки AS mvarchar), @КоличествоОшибок;");

            return script.ToString();
        }
        [TestMethod] public void SqlServer_SqlServer()
        {
            Dictionary<string, string> consumerOptions = new()
            {
                { "ConnectionString", MS_CONNECTION_STRING },
                { "SourceScript", GetSourceScript_SqlServer() },
                { "MessagesPerTransaction", "1000" }
            };
            Dictionary<string, string> producerOptions = new()
            {
                { "ConnectionString", MS_CONNECTION_STRING },
                { "TargetScript", GetTargetScript_SqlServer() }
            };

            SqlServer.Consumer consumer = new();
            SqlServer.Producer producer = new();
            consumer.LinkTo(producer);
            consumer.Configure(in consumerOptions);
            producer.Configure(in producerOptions);

            Stopwatch watch = new();
            watch.Start();
            consumer.Pump();
            watch.Stop();
            Console.WriteLine($"Elapsed {watch.ElapsedMilliseconds} ms");
        }
        [TestMethod] public void SqlServer_PostgreSql()
        {
            Dictionary<string, string> consumerOptions = new()
            {
                { "ConnectionString", MS_CONNECTION_STRING },
                { "SourceScript", GetSourceScript_SqlServer() },
                { "MessagesPerTransaction", "1000" }
            };
            Dictionary<string, string> producerOptions = new()
            {
                { "ConnectionString", PG_CONNECTION_STRING },
                { "TargetScript", GetTargetScript_PostgreSql() }
            };

            SqlServer.Consumer consumer = new();
            PostgreSql.Producer producer = new();
            consumer.LinkTo(producer);
            consumer.Configure(in consumerOptions);
            producer.Configure(in producerOptions);

            Stopwatch watch = new();
            watch.Start();
            consumer.Pump();
            watch.Stop();
            Console.WriteLine($"Elapsed {watch.ElapsedMilliseconds} ms");
        }
        [TestMethod] public void PostgreSql_SqlServer()
        {
            Dictionary<string, string> consumerOptions = new()
            {
                { "ConnectionString", PG_CONNECTION_STRING },
                { "SourceScript", GetSourceScript_PostgreSql() },
                { "MessagesPerTransaction", "1000" }
            };
            Dictionary<string, string> producerOptions = new()
            {
                { "ConnectionString", MS_CONNECTION_STRING },
                { "TargetScript", GetTargetScript_SqlServer() }
            };

            PostgreSql.Consumer consumer = new();
            SqlServer.Producer producer = new();
            consumer.LinkTo(producer);
            consumer.Configure(in consumerOptions);
            producer.Configure(in producerOptions);

            Stopwatch watch = new();
            watch.Start();
            consumer.Pump();
            watch.Stop();
            Console.WriteLine($"Elapsed {watch.ElapsedMilliseconds} ms");
        }
        [TestMethod] public void PostgreSql_PostgreSql()
        {
            Dictionary<string, string> consumerOptions = new()
            {
                { "ConnectionString", PG_CONNECTION_STRING },
                { "SourceScript", GetSourceScript_PostgreSql() },
                { "MessagesPerTransaction", "1000" }
            };
            Dictionary<string, string> producerOptions = new()
            {
                { "ConnectionString", PG_CONNECTION_STRING },
                { "TargetScript", GetTargetScript_PostgreSql() }
            };

            PostgreSql.Consumer consumer = new();
            PostgreSql.Producer producer = new();
            consumer.LinkTo(producer);
            consumer.Configure(in consumerOptions);
            producer.Configure(in producerOptions);

            Stopwatch watch = new();
            watch.Start();
            consumer.Pump();
            watch.Stop();
            Console.WriteLine($"Elapsed {watch.ElapsedMilliseconds} ms");
        }
    }
}