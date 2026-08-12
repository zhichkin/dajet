using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using DaJet.Runtime;
using System.Text;

namespace benchmark
{
    [Config(typeof(Config))]
    [MemoryDiagnoser]
    [MinColumn, MaxColumn]
    //[WarmupCount(1)]
    [IterationCount(10)]
    //[MinIterationCount(5)]
    //[MaxIterationCount(20)]
    public class DaJetScriptBenchmarks
    {
        private static IProcessor _processor;
        private static readonly Dictionary<string, object> _parameters = new();

        private class Config : ManualConfig
        {
            public Config()
            {
                AddJob(Job.Default.WithGcServer(true).WithGcForce(false).WithId("Server"));
                //AddJob(Job.Default.WithGcServer(false).WithGcForce(false).WithId("Workstation));

                //AddJob(Job.Default.WithGcServer(true).WithGcForce(true).WithId("ServerForce"));
                //AddJob(Job.Default.WithGcServer(false).WithGcForce(true).WithId("WorkstationForce""));
            }
        }

        [GlobalSetup]
        public void GlobalSetup()
        {
            //string filePath = Path.Combine(AppContext.BaseDirectory, "scripts", "select-complex-object.djs");

            string filePath = Path.Combine(AppContext.BaseDirectory, "scripts", "stream-pg-ms.djs");

            string script;

            using (StreamReader reader = new(filePath, Encoding.UTF8))
            {
                script = reader.ReadToEnd();
            }

            if (!StreamFactory.TryCreateStream(in script, in _parameters, out IProcessor processor, out string error))
            {
                throw new Exception(error);
            }

            _processor = processor;
        }

        [Benchmark(Description = "STREAM PG > MS")]
        public bool STREAM_PG_MS()
        {
            _processor.Process();

            return true;
        }
    }
}