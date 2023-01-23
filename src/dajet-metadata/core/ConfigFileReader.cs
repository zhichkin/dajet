using DaJet.Data;
using Microsoft.Data.SqlClient;
using Npgsql;
using System;
using System.Buffers;
using System.Data;
using System.Data.Common;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace DaJet.Metadata.Core
{
    /// <summary>
    /// Класс для чтения файлов конфигурации 1С из таблиц _YearOffset, IBVersion, Config и Params (file DBNames)
    /// </summary>
    public sealed class ConfigFileReader : IDisposable
    {
        #region "CONSTANTS"

        private const string ROOT_FILE_NAME = "root"; // Config
        private const string DBNAMES_FILE_NAME = "DBNames"; // Params

        private const string MS_PARAMS_QUERY_SCRIPT = "SELECT [BinaryData] FROM [Params] WHERE [FileName] = @FileName;";
        private const string MS_CONFIG_QUERY_SCRIPT = "SELECT [BinaryData] FROM [Config] WHERE [FileName] = @FileName;"; // Version 8.3 ORDER BY [PartNo] ASC";
        private const string MS_DBSCHEMA_QUERY_SCRIPT = "SELECT TOP 1 (CASE WHEN SUBSTRING(SerializedData, 1, 3) = 0xEFBBBF THEN 1 ELSE 0 END) AS UTF8, CAST(DATALENGTH(SerializedData) AS int) AS DataSize, SerializedData FROM DBSchema;";
        private const string MS_IBVERSION_QUERY_SCRIPT = "SELECT TOP 1 [PlatformVersionReq] FROM [IBVersion];";
        private const string MS_YEAROFFSET_QUERY_SCRIPT = "SELECT TOP 1 [Offset] FROM [_YearOffset];";
        private const string MS_SCHEMA_STORAGE_QUERY_SCRIPT = "SELECT (CASE WHEN SUBSTRING(CurrentSchema, 1, 3) = 0xEFBBBF THEN 1 ELSE 0 END) AS UTF8, CAST(DATALENGTH(CurrentSchema) AS int) AS DataSize, CurrentSchema FROM SchemaStorage WHERE SchemaID = 0;";

        private const string PG_PARAMS_QUERY_SCRIPT = "SELECT binarydata FROM params WHERE filename = '{filename}';";
        private const string PG_CONFIG_QUERY_SCRIPT = "SELECT binarydata FROM config WHERE filename = '{filename}';"; // Version 8.3 ORDER BY [PartNo] ASC";
        private const string PG_DBSCHEMA_QUERY_SCRIPT = "SELECT (CASE WHEN SUBSTRING(serializeddata, 1, 3) = E'\\\\xEFBBBF' THEN 1 ELSE 0 END) AS UTF8, CAST(OCTET_LENGTH(serializeddata) AS int) AS datasize, serializeddata FROM dbschema LIMIT 1;";
        private const string PG_IBVERSION_QUERY_SCRIPT = "SELECT platformversionreq FROM ibversion LIMIT 1;";
        private const string PG_YEAROFFSET_QUERY_SCRIPT = "SELECT ofset FROM _yearoffset LIMIT 1;";
        private const string PG_SCHEMA_STORAGE_QUERY_SCRIPT = "SELECT (CASE WHEN SUBSTRING(currentschema, 1, 3) = E'\\\\xEFBBBF' THEN 1 ELSE 0 END) AS UTF8, CAST(OCTET_LENGTH(currentschema) AS int) AS datasize, currentschema FROM schemastorage WHERE schemaid = 0;";

        private const string MS_PARAMS_SCRIPT = "SELECT (CASE WHEN SUBSTRING(BinaryData, 1, 3) = 0xEFBBBF THEN 1 ELSE 0 END) AS UTF8, CAST(DataSize AS int) AS DataSize, BinaryData FROM Params WHERE FileName = @FileName;";
        private const string MS_CONFIG_SCRIPT = "SELECT (CASE WHEN SUBSTRING(BinaryData, 1, 3) = 0xEFBBBF THEN 1 ELSE 0 END) AS UTF8, CAST(DataSize AS int) AS DataSize, BinaryData FROM Config WHERE FileName = @FileName;";
        private const string MS_CONFIG_CAS_SCRIPT = "SELECT (CASE WHEN SUBSTRING(BinaryData, 1, 3) = 0xEFBBBF THEN 1 ELSE 0 END) AS UTF8, CAST(DataSize AS int) AS DataSize, BinaryData FROM ConfigCAS WHERE FileName = @FileName;";

        private const string PG_PARAMS_SCRIPT = "SELECT (CASE WHEN SUBSTRING(binarydata, 1, 3) = E'\\\\xEFBBBF' THEN 1 ELSE 0 END) AS UTF8, CAST(datasize AS int) AS datasize, binarydata FROM params WHERE LOWER(CAST(filename AS varchar)) = @filename;";
        private const string PG_CONFIG_SCRIPT = "SELECT (CASE WHEN SUBSTRING(binarydata, 1, 3) = E'\\\\xEFBBBF' THEN 1 ELSE 0 END) AS UTF8, CAST(datasize AS int) AS datasize, binarydata FROM config WHERE LOWER(CAST(filename AS varchar)) = @filename;";
        private const string PG_CONFIG_CAS_SCRIPT = "SELECT (CASE WHEN SUBSTRING(binarydata, 1, 3) = E'\\\\xEFBBBF' THEN 1 ELSE 0 END) AS UTF8, CAST(datasize AS int) AS datasize, binarydata FROM configcas WHERE LOWER(CAST(filename AS varchar)) = @filename;";

        #endregion

        #region "PRIVATE FIELDS"

        private readonly string _fileName;
        private readonly string _connectionString;
        private readonly DatabaseProvider _provider;

        private byte[] _buffer;
        private int _bufferSize;
        private bool _utf8 = false;
        private StreamReader _stream;
        private int _offset = -1;
        private int _level = -1;
        private int[] _path = new int[16];
        private char _char = char.MinValue;
        private TokenType _token = TokenType.None;
        private StringBuilder _value = new StringBuilder(256);
        private bool _valueIsNull = false;

        #endregion

        public string FileName { get { return _fileName; } }
        public StreamReader Stream { get { return _stream; } }
        public string ConnectionString { get { return _connectionString; } }
        public DatabaseProvider DatabaseProvider { get { return _provider; } }

        #region "STATIC MEMBERS"

        public static StreamReader Create(in string connectionString, in string tableName, in string fileName)
        {
            DatabaseProvider provider = connectionString.StartsWith("Host")
                ? DatabaseProvider.PostgreSql
                : DatabaseProvider.SqlServer;

            byte[] fileData = ExecuteDbReader(provider, in connectionString, in tableName, in fileName);

            if (fileData == null)
            {
                fileData = Array.Empty<byte>();
            }

            return CreateReader(in fileData);
        }
        private static bool IsUTF8(in byte[] fileData)
        {
            if (fileData == null) throw new ArgumentNullException(nameof(fileData));

            if (fileData.Length < 3) return false;

            return fileData[0] == 0xEF  // (b)yte
                && fileData[1] == 0xBB  // (o)rder
                && fileData[2] == 0xBF; // (m)ark
        }
        private static byte[] CombineArrays(byte[] a1, byte[] a2)
        {
            if (a1 == null) return a2;

            byte[] result = new byte[a1.Length + a2.Length];

            Buffer.BlockCopy(a1, 0, result, 0, a1.Length);
            Buffer.BlockCopy(a2, 0, result, a1.Length, a2.Length);

            return result;
        }
        private static StreamReader CreateReader(in byte[] fileData)
        {
            MemoryStream memory = new(fileData);

            if (IsUTF8(in fileData))
            {
                return new StreamReader(memory, Encoding.UTF8);
            }

            DeflateStream stream = new(memory, CompressionMode.Decompress);

            return new StreamReader(stream, Encoding.UTF8);
        }
        private static string ConfigureDatabaseScript(DatabaseProvider provider, in string tableName)
        {
            if (tableName == ConfigTables.Config)
            {
                if (provider == DatabaseProvider.SqlServer)
                {
                    return MS_CONFIG_SCRIPT;
                }
                return PG_CONFIG_SCRIPT;
            }
            else if (tableName == ConfigTables.ConfigCAS)
            {
                if (provider == DatabaseProvider.SqlServer)
                {
                    return MS_CONFIG_CAS_SCRIPT;
                }
                return PG_CONFIG_CAS_SCRIPT;
            }
            else if (tableName == ConfigTables.Params)
            {
                if (provider == DatabaseProvider.SqlServer)
                {
                    return MS_PARAMS_SCRIPT;
                }
                return PG_PARAMS_SCRIPT;
            }
            return null;
        }
        private static DbConnection CreateDbConnection(DatabaseProvider provider, in string connectionString)
        {
            if (provider == DatabaseProvider.SqlServer)
            {
                return new SqlConnection(connectionString);
            }
            return new NpgsqlConnection(connectionString);
        }
        private static void ConfigureDbParameters(in DbCommand command, in string fileName)
        {
            if (command is SqlCommand ms_cmd)
            {
                ms_cmd.Parameters.AddWithValue("FileName", fileName);
            }
            else if (command is NpgsqlCommand pg_cmd)
            {
                pg_cmd.Parameters.AddWithValue("filename", fileName); ;
            }
        }
        private static byte[] ExecuteDbReader(DatabaseProvider provider, in string connectionString, in string tableName, in string fileName)
        {
            byte[] fileData = null;

            string script = ConfigureDatabaseScript(provider, tableName);

            using (DbConnection connection = CreateDbConnection(provider, in connectionString))
            {
                connection.Open();

                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = script;
                    command.CommandType = CommandType.Text;
                    command.CommandTimeout = 10; // seconds

                    ConfigureDbParameters(in command, in fileName);

                    using (DbDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            bool utf8 = (reader.GetInt32(0) == 1);
                            int size = reader.GetInt32(1);
                            byte[] data = (byte[])reader[2];
                            
                            fileData = CombineArrays(fileData, data);
                        }
                    }
                }
            }

            return fileData;
        }
        
        #endregion

        public ConfigFileReader(StreamReader stream)
        {
            InitializePath();
            _stream = stream;
        }
        public ConfigFileReader(DatabaseProvider provider, in string connectionString, in string tableName)
        {
            if (!(tableName == ConfigTables.DBSchema
                || tableName == ConfigTables.SchemaStorage))
            {
                throw new ArgumentOutOfRangeException(nameof(tableName) + " = " + tableName);
            }

            InitializePath();

            _provider = provider;
            _connectionString = connectionString;

            YearOffset = GetYearOffset();
            PlatformVersion = GetPlatformVersion();

            long bytes = 0L;

            if (tableName == ConfigTables.DBSchema)
            {
                bytes = ExecuteReader(
                    (provider == DatabaseProvider.SqlServer
                    ? MS_DBSCHEMA_QUERY_SCRIPT
                    : PG_DBSCHEMA_QUERY_SCRIPT)
                    , null);
            }
            else if (tableName == ConfigTables.SchemaStorage)
            {
                bytes = ExecuteReader(
                    (provider == DatabaseProvider.SqlServer
                    ? MS_SCHEMA_STORAGE_QUERY_SCRIPT
                    : PG_SCHEMA_STORAGE_QUERY_SCRIPT)
                    , null);
            }

            if (bytes == 0L)
            {
                _buffer = Array.Empty<byte>();
            }

            _stream = CreateReader(in _buffer, _utf8);
        }
        public ConfigFileReader(DatabaseProvider provider, in string connectionString, in string tableName, Guid fileUuid)
            : this(provider, in connectionString, in tableName, fileUuid.ToString())
        {
            //  Convenience constructor
        }
        public ConfigFileReader(DatabaseProvider provider, in string connectionString, in string tableName, in string fileName)
        {
            InitializePath();

            _fileName = fileName;
            _provider = provider;
            _connectionString = connectionString;

            YearOffset = GetYearOffset();
            PlatformVersion = GetPlatformVersion();

            string script = GetSelectConfigFileScript(tableName);

            long bytes = 0L;

            if (provider == DatabaseProvider.PostgreSql)
            {
                bytes = ExecuteReader(script, fileName.ToLower());
            }
            else
            {
                bytes = ExecuteReader(script, fileName);
            }

            if (bytes == 0)
            {
                _buffer = Array.Empty<byte>();
            }

            _stream = CreateReader(in _buffer, _utf8);
        }
        private void InitializePath()
        {
            for (int i = 0; i < _path.Length; i++)
            {
                _path[i] = -1;
            }
        }
        public void Dispose()
        {
            if (_buffer != null)
            {
                ArrayPool<byte>.Shared.Return(_buffer);
                _buffer = null;
            }

            if (_stream != null)
            {
                _stream.Dispose();
                _stream = null;
            }
        }

        public int YearOffset { get; private set; }
        public int PlatformVersion { get; private set; }

        //private readonly RecyclableMemoryStreamManager MemoryPool = new RecyclableMemoryStreamManager();

        #region "DATABASE READER CODE"

        private string GetSelectConfigFileScript(string tableName)
        {
            if (tableName == ConfigTables.Config)
            {
                if (_provider == DatabaseProvider.SqlServer)
                {
                    return MS_CONFIG_SCRIPT;
                }
                return PG_CONFIG_SCRIPT;
            }
            else if (tableName == ConfigTables.ConfigCAS)
            {
                if (_provider == DatabaseProvider.SqlServer)
                {
                    return MS_CONFIG_CAS_SCRIPT;
                }
                return PG_CONFIG_CAS_SCRIPT;
            }
            else if (tableName == ConfigTables.Params)
            {
                if (_provider == DatabaseProvider.SqlServer)
                {
                    return MS_PARAMS_SCRIPT;
                }
                return PG_PARAMS_SCRIPT;
            }
            return null;
        }

        private DbConnection CreateDbConnection()
        {
            if (_provider == DatabaseProvider.SqlServer)
            {
                return new SqlConnection(_connectionString);
            }
            return new NpgsqlConnection(_connectionString);
        }
        private void ConfigureFileNameParameter(DbCommand command, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return;

            if (_provider == DatabaseProvider.SqlServer)
            {
                ((SqlCommand)command).Parameters.AddWithValue("FileName", fileName);
            }
            else
            {
                ((NpgsqlCommand)command).Parameters.AddWithValue("filename", fileName);
            }
        }
        private T ExecuteScalar<T>(string script, string fileName)
        {
            T result = default(T);
            using (DbConnection connection = CreateDbConnection())
            using (DbCommand command = connection.CreateCommand())
            {
                command.CommandText = script;
                command.CommandType = CommandType.Text;
                ConfigureFileNameParameter(command, fileName);
                connection.Open();
                object value = command.ExecuteScalar();
                if (value != null)
                {
                    result = (T)value;
                }
            }
            return result;
        }
        private long ExecuteReader(string script, string fileName)
        {
            long bytesRead = 0;

            using (DbConnection connection = CreateDbConnection())
            {
                connection.Open();

                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = script;
                    command.CommandType = CommandType.Text;
                    command.CommandTimeout = 10; // seconds

                    ConfigureFileNameParameter(command, fileName);

                    using (DbDataReader reader = command.ExecuteReader(CommandBehavior.SequentialAccess))
                    {
                        while (reader.Read())
                        {
                            _utf8 = (reader.GetInt32(0) == 1);
                            _bufferSize = reader.GetInt32(1);
                            _buffer = ArrayPool<byte>.Shared.Rent(_bufferSize);
                            bytesRead = reader.GetBytes(2, 0L, _buffer, 0, _bufferSize);

                            //byte[] data = (byte[])reader[0];
                            //fileData = CombineArrays(fileData, data);
                        }
                    }
                }
            }

            return bytesRead;
        }
        
        private int GetYearOffset()
        {
            if (_provider == DatabaseProvider.SqlServer)
            {
                return ExecuteScalar<int>(MS_YEAROFFSET_QUERY_SCRIPT, null);
            }
            return ExecuteScalar<int>(PG_YEAROFFSET_QUERY_SCRIPT, null);
        }
        private int GetPlatformVersion()
        {
            if (_provider == DatabaseProvider.SqlServer)
            {
                return ExecuteScalar<int>(MS_IBVERSION_QUERY_SCRIPT, null);
            }
            return ExecuteScalar<int>(PG_IBVERSION_QUERY_SCRIPT, null);
        }

        #endregion

        private StreamReader CreateReader(in byte[] fileData, bool utf8)
        {
            if (utf8)
            {
                return CreateStreamReader(in fileData);
            }
            return CreateDeflateReader(in fileData);
        }
        private StreamReader CreateStreamReader(in byte[] fileData)
        {
            MemoryStream memory = new MemoryStream(fileData);
            return new StreamReader(memory, Encoding.UTF8);
        }
        private StreamReader CreateDeflateReader(in byte[] fileData)
        {
            MemoryStream memory = new MemoryStream(fileData);
            DeflateStream stream = new DeflateStream(memory, CompressionMode.Decompress);
            return new StreamReader(stream, Encoding.UTF8);
        }

        #region "CONFIG FILE READER IMPLEMENTATION"

        public char Char { get { return _char; } }
        public int Offset { get { return _offset; } }
        public int Level { get { return _level; } }
        public int[] Path { get { return _path; } }
        public int ValuePointer
        {
            get
            {
                return (_token == TokenType.StartObject
                    ? _path[_level - 1] // value pointer of the current object is -1, but the current object itself is the value of the previous (parent) object
                    : _path[_level]);
            }
        }
        public string Value { get { return (_valueIsNull ? null : _value.ToString()); } }
        public TokenType Token { get { return _token; } }
        public Guid GetUuid()
        {
            if (Value == null)
            {
                return Guid.Empty;
            }

            try
            {
                return new Guid(Value);
            }
            catch
            {
                return Guid.Empty;
            }
        }
        public int GetInt32()
        {
            if (Value == null)
            {
                return -1;
            }

            try
            {
                return int.Parse(Value);
            }
            catch
            {
                return -1;
            }
        }

        public bool Read()
        {
            if (_stream.EndOfStream)
            {
                return false;
            }

            if (_char == '{' && IsNullValueNext())
            {
                ReadNullValue();
                return true;
            }

            while (!_stream.EndOfStream)
            {
                _char = (char)_stream.Read();
                _offset++;

                if (IgnoreChar())
                {
                    continue;
                }

                if (_char == '{')
                {
                    ReadStartFileOrObject();
                }
                else if (_char == '}')
                {
                    ReadEndFileOrObject();
                }
                else if (_char == '"')
                {
                    ReadStringValue();
                }
                else if (_char == ',')
                {
                    if (IsNullValueNext())
                    {
                        ReadNullValue();
                        return true;
                    }

                    continue;
                }
                else
                {
                    ReadValue();
                }

                return true; // stop reading of the stream and return token, which has been just consumed
            }

            return false;
        }
        private char PeekNext()
        {
            while (!_stream.EndOfStream)
            {
                char next = (char)_stream.Peek();

                if (next == '\n' || next == '\r' || next == ' ')
                {
                    _ = _stream.Read();
                    _offset++;
                    continue;
                }

                return next;
            }

            return char.MinValue;
        }
        private bool IgnoreChar()
        {
            return (_char == '\n' || _char == '\r' || _char == ' ');
        }
        private void ReadStartFileOrObject()
        {
            if (_level > -1) // -1 level has no values
            {
                _path[_level]++; // increment pointer to the value at the current level
            }

            _level++; // increment pointer to the level (next nested object)

            _valueIsNull = false; // current value is object
            _value.Clear(); // clear value buffer

            _token = (_level == 0 ? TokenType.StartFile : TokenType.StartObject);
        }
        private void ReadEndFileOrObject()
        {
            _valueIsNull = false; // current value is object
            _value.Clear(); // clear value buffer
            
            _path[_level] = -1; // annul pointer to the value at the current level
            
            _level--; // decrement pointer to the level - point to the previous object (level)

            _token = (_level < 0 ? TokenType.EndFile : TokenType.EndObject);
        }
        private void ReadValue()
        {
            _valueIsNull = false;
            _value.Clear();

            _value.Append(_char);

            char next;

            while (!_stream.EndOfStream)
            {
                next = PeekNext();

                if (next == '}')
                {
                    _path[_level]++;
                    _token = TokenType.Value;
                    return;
                }

                if (next == ',')
                {
                    _path[_level]++; // point to the value, which has been just read, at the current level
                    _token = TokenType.Value;
                    return;
                }

                _char = (char)_stream.Read();
                _offset++;

                _value.Append(_char);
            }

            throw new FormatException("Unexpected end of file");
        }
        private void ReadNullValue()
        {
            // examples of null values:   {}   {,   ,,   ,}
            _valueIsNull = true;
            _value.Clear();
            _path[_level]++; // point to the value, which has been just read, at the current level
            _char = char.MinValue;
            _token = TokenType.Value;
        }
        private bool IsNullValueNext()
        {
            char next = PeekNext();

            if (next == char.MinValue)
            {
                throw new FormatException("Unexpected end of file");
            }

            return (next == ',' || next == '}');
        }
        private void ReadStringValue()
        {
            if (_char != '"')
            {
                throw new FormatException("Unexpected state");
            }

            _valueIsNull = false;
            _value.Clear();

            while (!_stream.EndOfStream)
            {
                _char = (char)_stream.Read();
                _offset++;

                if (_char == '"')
                {
                    if ((char)_stream.Peek() == '"')
                    {
                        _char = (char)_stream.Read();
                        _offset++;
                        
                        _value.Append(_char);

                        continue;
                    }
                    else
                    {
                        _path[_level]++; // point to the value, which has been just read, at the current level
                        _token = TokenType.String;
                        break;
                    }
                }

                _value.Append(_char);
            }
        }

        #endregion
    }
}