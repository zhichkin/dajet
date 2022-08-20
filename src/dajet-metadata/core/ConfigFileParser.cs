using System.ComponentModel;

namespace DaJet.Metadata.Core
{
    public delegate void ConfigFileTokenHandler(in ConfigFileReader source, in CancelEventArgs args);

    public sealed class ConfigFileParser
    {
        private readonly CancelEventArgs _args = new CancelEventArgs(false);
        
        public ConfigObject Parse(in ConfigFileReader reader)
        {
            ConfigObject config = new ConfigObject();
            ParseFile(in config, in reader);
            return config;
        }
        private void ParseFile(in ConfigObject parent, in ConfigFileReader reader)
        {
            while (reader.Read())
            {
                if (reader.Token == TokenType.StartFile)
                {
                    continue;
                }
                else if (reader.Token == TokenType.StartObject)
                {
                    ConfigObject config = new ConfigObject();
                    ParseFile(in config, in reader);
                    parent.Values.Add(config);
                }
                else if (reader.Token == TokenType.EndObject)
                {
                    return;
                }
                else if (reader.Token == TokenType.EndFile)
                {
                    return;
                }
                else
                {
                    parent.Values.Add(reader.Value);
                }
            }
        }

        public void Parse(in ConfigFileReader source, in ConfigFileConverter converter)
        {
            _args.Cancel = false;

            while (source.Read())
            {
                if (source.Token == TokenType.StartFile)
                {
                    converter.Root.TokenHandler?.Invoke(in source, in _args);
                    if (_args.Cancel) { break; }
                }
                else if (source.Token == TokenType.EndFile)
                {
                    converter.Root.TokenHandler?.Invoke(in source, in _args);
                    break;
                }
                else if (source.Token == TokenType.StartObject)
                {
                    // synchronize converter with source and invoke token handler if present
                    // after token handler has been invoked converter and source may get out of sync
                    converter.GetTokenHandler(source.Level - 1, source.Path)?.Invoke(in source, in _args);
                    if (_args.Cancel) { break; }
                }
                else if (source.Token == TokenType.EndObject)
                {
                    // synchronize converter with source and invoke token handler if present
                    // after token handler has been invoked converter and source may get out of sync
                    converter.GetTokenHandler(source.Level, source.Path)?.Invoke(in source, in _args);
                    if (_args.Cancel) { break; }
                }
                else if (source.Token == TokenType.Value || source.Token == TokenType.String)
                {
                    // synchronize converter with source and invoke token handler if present
                    // after token handler has been invoked converter and source may get out of sync
                    converter.GetTokenHandler(source.Level, source.Path)?.Invoke(in source, in _args);
                    if (_args.Cancel) { break; }
                }
            }
        }
    }
}