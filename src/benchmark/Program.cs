using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace benchmark
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            Summary summary = BenchmarkRunner.Run<ConfigFileParserBenchmarks>();
            //Summary summary = BenchmarkRunner.Run<MetadataProviderBenchmarks>();
        }
    }
}