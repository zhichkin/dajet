using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using DaJet.Metadata;

namespace benchmark
{
    [Config(typeof(Config))]
    //[MemoryDiagnoser]
    [MinColumn, MaxColumn]
    //[WarmupCount(1)]
    //[IterationCount(1)]
    //[MinIterationCount(5)]
    //[MaxIterationCount(20)]
    public class MetadataProviderBenchmarks
    {
        private static readonly string MS_CONNECTION = "Data Source=ZHICHKIN;Initial Catalog=unf;Integrated Security=True;Encrypt=False;";
        private static readonly string PG_CONNECTION = "Host=127.0.0.1;Port=5432;Database=unf;Username=postgres;Password=postgres;";

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

        [Benchmark(Description = "PostgreSQL")]
        public OneDbMetadataProvider PG_InitializeMetadataCache()
        {
            return new OneDbMetadataProvider(PG_CONNECTION, true);
        }

        //[Benchmark(Description = "SQL Server")]
        public OneDbMetadataProvider MS_InitializeMetadataCache()
        {
             return new OneDbMetadataProvider(MS_CONNECTION, true);
        }
    }
}