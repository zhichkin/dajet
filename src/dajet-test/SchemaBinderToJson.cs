using DaJet.Metadata;
using DaJet.Scripting.Model;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace DaJet.Scripting.Test
{
    [TestClass] public class SchemaBinderToJson
    {
        private static readonly string MS_CONNECTION = "Data Source=ZHICHKIN;Initial Catalog=dajet-metadata-ms;Integrated Security=True;Encrypt=False;";
        private static readonly string PG_CONNECTION = "Host=127.0.0.1;Port=5432;Database=dajet-metadata-pg;Username=postgres;Password=postgres;";
        private static readonly List<string> scripts = new()
        {
            "C:\\temp\\scripting-test\\script-lateral-join.txt",
            "C:\\temp\\scripting-test\\test-new-schema-binder.txt"
        };
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        static SchemaBinderToJson()
        {
            JsonOptions.Converters.Add(new SyntaxNodeJsonConverter());
            JsonOptions.Converters.Add(new ScriptScopeJsonConverter());
        }
        private void WriteScriptScope(in ScriptScope scope)
        {
            string outputFile = "C:\\temp\\scripting-test\\script-scope-output.json";

            try
            {
                string json = JsonSerializer.Serialize(scope, JsonOptions);

                using (StreamWriter writer = new(outputFile, false, Encoding.UTF8))
                {
                    writer.Write(json);
                }
            }
            catch (Exception error)
            {
                Console.WriteLine(ExceptionHelper.GetErrorMessage(error));
            }
        }
        private void WriteScriptModel(in ScriptModel model)
        {
            string outputFile = "C:\\temp\\scripting-test\\script-model-output.json";

            try
            {
                string json = JsonSerializer.Serialize(model, typeof(SyntaxNode), JsonOptions);

                using (StreamWriter writer = new(outputFile, false, Encoding.UTF8))
                {
                    writer.Write(json);
                }
            }
            catch (Exception error)
            {
                Console.WriteLine(ExceptionHelper.GetErrorMessage(error));
            }
        }
        [TestMethod] public void Bind_Correlated_Subquery()
        {
            string script;
            using (StreamReader reader = new(scripts[0], Encoding.UTF8))
            {
                script = reader.ReadToEnd();
            }
            
            IMetadataProvider metadata = new OneDbMetadataProvider(MS_CONNECTION);
            //IMetadataProvider metadata = new OneDbMetadataProvider(PG_CONNECTION);

            if (!new ScriptParser().TryParse(in script, out ScriptModel model, out string error))
            {
                Console.WriteLine(error);
            }

            if (!new MetadataBinder().TryBind(model, in metadata, out ScriptScope scope, out List<string> errors))
            {
                foreach (string text in errors)
                {
                    Console.WriteLine(text);
                }
            }

            if (!new ScriptTransformer().TryTransform(model, out error))
            {
                Console.WriteLine(error);
            }

            WriteScriptScope(in scope);
            WriteScriptModel(in model);

            Console.WriteLine("Success");
        }
        [TestMethod] public void Bind_No_Correlated_Subquery()
        {
            string script;
            using (StreamReader reader = new(scripts[1], Encoding.UTF8))
            {
                script = reader.ReadToEnd();
            }

            IMetadataProvider metadata = new OneDbMetadataProvider(MS_CONNECTION);
            //IMetadataProvider metadata = new OneDbMetadataProvider(PG_CONNECTION);

            if (!new ScriptParser().TryParse(in script, out ScriptModel model, out string error))
            {
                Console.WriteLine(error);
            }

            if (!new MetadataBinder().TryBind(model, in metadata, out ScriptScope scope, out List<string> errors))
            {
                foreach (string text in errors)
                {
                    Console.WriteLine(text);
                }
            }

            if (!new ScriptTransformer().TryTransform(model, out error))
            {
                Console.WriteLine(error);
            }

            WriteScriptScope(in scope);
            WriteScriptModel(in model);

            Console.WriteLine("Success");
        }
    }
}