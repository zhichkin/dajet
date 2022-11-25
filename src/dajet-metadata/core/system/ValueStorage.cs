using DaJet.Metadata.Core;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace DaJet.Data
{
    public sealed class ValueStorage
    {
        private readonly byte[] _data;
        private readonly int _null_char = 0x00;
        private readonly string _utf8 = "0xEFBBBF"; // 3 bytes
        private readonly string _header0 = "0x0101"; // 2 bytes { 0x0101 - plain, 0x0201 - deflate }
        private readonly string _header1 = "0x3D00000000000000"; // 8 bytes
        private readonly string _header2 = "0x534B6FF4888DC14EA0D5EBB6BDA0A70D"; // 16 bytes
        private readonly string _header3 = "0x000000000F00000000000000"; // 12 bytes
        private string _type = string.Empty;
        public ValueStorage(byte[] data)
        {
            _data = data;
        }
        public string ValueType { get { return _type; } }
        public StreamReader GetDataAsStream()
        {
            StreamReader stream = CreateStream();
            return ParseConfigObject(in stream);
        }
        private StreamReader CreateStream()
        {
            int offset = 0;

            if (_data[0] == 1) // plain stream
            {
                offset = 2;
            }
            else if (_data[0] == 2) // deflate stream
            {
                offset = 18;
            }

            MemoryStream memory = new(_data, offset, _data.Length - offset);

            if (offset == 2)
            {
                return new StreamReader(memory, Encoding.UTF8);
            }
            else if (offset == 18)
            {
                DeflateStream stream = new(memory, CompressionMode.Decompress);
                return new StreamReader(stream, Encoding.UTF8);
            }
            else
            {
                return new StreamReader(memory);
            }
        }
        private StreamReader ParseConfigObject(in StreamReader stream)
        {
            while (!stream.EndOfStream) // 0x3D00000000000000
            {
                if (stream.Read() == 0xFEFF) // 0xEFBBBF - BOM || 0xFEFF - ZERO WIDTH NO-BREAK SPACE
                {
                    break;
                }
            }

            if (stream.EndOfStream)
            {
                return stream;
            }

            ConfigFileReader reader = new(stream);

            ConfigObject config = new ConfigFileParser().Parse(in reader);

            _type = config.GetString(1);

            while (!stream.EndOfStream) // 0x000000000F00000000000000
            {
                if (stream.Read() == 0xFEFF) // 0xEFBBBF - BOM || 0xFEFF - ZERO WIDTH NO-BREAK SPACE
                {
                    break;
                }
            }

            if (stream.EndOfStream)
            {
                return stream;
            }

            return stream;
        }
    }
}