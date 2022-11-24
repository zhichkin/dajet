using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using System.Collections.Generic;
using System.ComponentModel;

namespace DaJet.Metadata.Parsers
{
    public sealed class TemplateCollectionParser
    {
        private readonly MetadataCache _cache;
        public TemplateCollectionParser(MetadataCache cache)
        {
            _cache = cache;
        }
        private Template _template;
        public void Parse(in ConfigFileReader reader, out List<Template> templates)
        {
            templates = new List<Template>();

            // Параметр reader в данный момент должен быть позиционирован
            // на узле коллекции свойств объекта метаданных (токен = '{')
            // reader.Char == '{' && reader.Token == TokenType.StartObject

            if (reader.Token != TokenType.StartObject)
            {
                return;
            }

            ParseTemplateUuids(in reader, in templates);

            ParseTemplateNames(in templates);

            _template = null;
        }
        private void ParseTemplateUuids(in ConfigFileReader reader, in List<Template> templates)
        {
            if (reader.Read() && reader.Token == TokenType.Value &&
                reader.GetUuid() == SystemUuid.Template_Collection)
            {
                if (reader.Read() && reader.Token == TokenType.Value)
                {
                    int count = reader.GetInt32();

                    for (int i = 0; i < count; i++)
                    {
                        if (reader.Read())
                        {
                            templates.Add(new Template()
                            {
                                Uuid = reader.GetUuid()
                            });
                        }
                    }
                }
            }

            if (reader.Token != TokenType.EndObject)
            {
                while (reader.Read())
                {
                    if (reader.Token == TokenType.EndObject)
                    {
                        break;
                    }
                }
            }
        }
        private void ParseTemplateNames(in List<Template> templates)
        {
            if (templates.Count == 0)
            {
                return;
            }

            ConfigFileParser parser = new();
            ConfigFileConverter converter = new();

            converter[1][1] += Type;
            converter[1][2][2] += Name;
            converter[1][2][4] += Comment;
            converter[1][2][3][2] += Alias;
            //converter[1][2][1][2] += Uuid;

            for (int i = 0; i < templates.Count; i++)
            {
                _template = templates[i];

                using (ConfigFileReader reader = new(_cache.DatabaseProvider, _cache.ConnectionString, ConfigTables.Config, _template.Uuid))
                {
                    parser.Parse(in reader, in converter);
                }
            }
        }
        private void Type(in ConfigFileReader source, in CancelEventArgs args)
        {
            _template.Type = (TemplateType)source.GetInt32();
        }
        private void Name(in ConfigFileReader source, in CancelEventArgs args)
        {
            _template.Name = source.Value;
        }
        private void Alias(in ConfigFileReader source, in CancelEventArgs args)
        {
            _template.Alias = source.Value;
        }
        private void Comment(in ConfigFileReader source, in CancelEventArgs args)
        {
            _template.Comment = source.Value;
        }
    }
}