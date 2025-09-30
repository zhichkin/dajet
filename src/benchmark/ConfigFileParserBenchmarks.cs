using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using DaJet.Metadata.Parsers;
using Microsoft.Data.SqlClient;
using Microsoft.IO;
using System.Buffers;
using System.Data;
using System.IO.Compression;
using System.Text;

namespace benchmark
{
    [Config(typeof(Config))]
    [MemoryDiagnoser]
    [MinColumn, MaxColumn]
    //[WarmupCount(1)]
    //[IterationCount(1)]
    //[MinIterationCount(5)]
    //[MaxIterationCount(20)]
    public class ConfigFileParserBenchmarks
    {
        private static readonly RecyclableMemoryStreamManager _memory = new();
        private static readonly string MS_CONNECTION = "Data Source=ZHICHKIN;Initial Catalog=unf;Integrated Security=True;Encrypt=False;";
        private static readonly string PG_CONNECTION = "Host=127.0.0.1;Port=5432;Database=unf;Username=postgres;Password=postgres;";
        private const string MS_PARAMS_SCRIPT = "SELECT (CASE WHEN SUBSTRING(BinaryData, 1, 3) = 0xEFBBBF THEN 1 ELSE 0 END) AS UTF8, CAST(DataSize AS int) AS DataSize, BinaryData FROM Params WHERE FileName = @FileName;";
        private const string MS_CONFIG_SCRIPT = "SELECT (CASE WHEN SUBSTRING(BinaryData, 1, 3) = 0xEFBBBF THEN 1 ELSE 0 END) AS UTF8, CAST(DataSize AS int) AS DataSize, BinaryData FROM Config WHERE FileName = @FileName;";
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

        private static bool _utf8 = false;
        private static int _size = 0; // initial size
        private static byte[] _data; // database data
        private static byte[] _buffer; // decompressed
        private static Guid _uuid = new("46bd4919-eaaa-4d20-9448-1c15fffa60a4");

        private static CatalogParser _parser = new();

        [GlobalSetup]
        public void GlobalSetup()
        {
            GetConfigFileData("46bd4919-eaaa-4d20-9448-1c15fffa60a4"); // Справочник
            DecompressConfigFile();
        }
        [GlobalCleanup]
        public void GlobalCleanup()
        {
            _buffer = null;
            ArrayPool<byte>.Shared.Return(_data);
        }
        private static SqlConnection CreateDbConnection()
        {
            return new SqlConnection(MS_CONNECTION);
        }
        private static void GetConfigFileData(string fileName)
        {
            using (SqlConnection connection = CreateDbConnection())
            {
                connection.Open();

                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = MS_CONFIG_SCRIPT;
                    command.CommandType = CommandType.Text;
                    command.CommandTimeout = 10; // seconds

                    command.Parameters.AddWithValue("FileName", fileName);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            bool utf8 = (reader.GetInt32(0) == 1);
                            _size = reader.GetInt32(1);
                            _data = ArrayPool<byte>.Shared.Rent(_size);
                            long loaded = reader.GetBytes(2, 0, _data, 0, _size);
                            Console.WriteLine($"Loaded = {loaded}");
                        }
                    }
                }
            }
        }
        private static void DecompressConfigFile()
        {
            if (_utf8)
            {
                _buffer = _data; return;
            }

            Span<byte> buffer = stackalloc byte[1024];

            using (MemoryStream source = new(_data, 0, _size, false, true))
            {
                using (DeflateStream deflate = new(source, CompressionMode.Decompress))
                {
                    using (RecyclableMemoryStream memory = _memory.GetStream())
                    {
                        int decompressed = 0;
                        do
                        {
                            decompressed = deflate.Read(buffer);
                            memory.Write(buffer[..decompressed]);
                        }
                        while (decompressed > 0);

                        _buffer = memory.GetReadOnlySequence().ToArray();

                        Console.WriteLine($"Decompressed = {memory.Length}");
                    }
                }
            }
        }

        [Benchmark(Description = "Справочник")]
        public MetadataObject CatalogParser()
        {
            MetadataObject metadata;

            using (RecyclableMemoryStream memory = _memory.GetStream(_buffer))
            {
                using (StreamReader stream = new(memory, Encoding.UTF8))
                {
                    using (ConfigFileReader reader = new(stream))
                    {
                        _parser.Parse(in reader, _uuid, out metadata);
                    }
                }
            }

            return metadata;
        }
    }
}