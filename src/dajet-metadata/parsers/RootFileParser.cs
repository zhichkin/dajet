using DaJet.Metadata.Core;
using System;
using System.ComponentModel;

namespace DaJet.Metadata.Parsers
{
    public sealed class RootFileParser
    {
        private Guid _target = Guid.Empty;
        private ConfigFileParser _parser;
        private ConfigFileConverter _converter;
        public Guid Parse(in ConfigFileReader reader)
        {
            _parser = new ConfigFileParser();

            ConfigureConverter();

            _parser.Parse(in reader, in _converter);

            _parser = null;
            _converter = null;

            return _target;
        }
        private void ConfigureConverter()
        {
            _converter = new ConfigFileConverter();

            _converter[1] += Uuid;
        }
        private void Uuid(in ConfigFileReader source, in CancelEventArgs args)
        {
            _target = source.GetUuid();

            args.Cancel = true;
        }
    }
}