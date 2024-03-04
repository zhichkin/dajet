using DaJet.Scripting;
using DaJet.Scripting.Model;
using System.Diagnostics;
using System.Text;

namespace DaJet.Stream
{
    public static class StreamManager
    {
        public static void Process(in string script)
        {
            Stopwatch watch = new();

            watch.Start();

            IProcessor stream = CreateStream(in script);

            watch.Stop();

            long elapsed = watch.ElapsedMilliseconds;

            Console.WriteLine($"Pipeline assembled in {elapsed} ms");

            watch.Restart();

            stream.Process();

            watch.Stop();

            elapsed = watch.ElapsedMilliseconds;

            Console.WriteLine($"Pipeline executed in {elapsed} ms");
        }
        private static string FormatErrorMessage(in List<string> errors)
        {
            if (errors is null || errors.Count == 0)
            {
                return "Unknown binding error";
            }

            StringBuilder error = new();

            for (int i = 0; i < errors.Count; i++)
            {
                if (i > 0) { error.AppendLine(); }

                error.Append(errors[i]);
            }

            return error.ToString();
        }
        private static IProcessor CreateStream(in string script)
        {
            ScriptModel model;

            using (ScriptParser parser = new())
            {
                if (!parser.TryParse(in script, out model, out string error))
                {
                    Console.WriteLine(error);
                }
            }

            return StreamFactory.Create(in model);
        }
    }
}