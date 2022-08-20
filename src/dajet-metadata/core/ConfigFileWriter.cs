using System.IO;
using System.Text;

namespace DaJet.Metadata.Core
{
    public sealed class ConfigFileWriter
    {
        public void Write(ConfigObject config, string filePath)
        {
            using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                WriteToFile(writer, config, 0, string.Empty);
            }
        }
        private void WriteToFile(StreamWriter writer, ConfigObject config, int level, string path)
        {
            string indent = level == 0 ? string.Empty : "-".PadLeft(level * 4, '-');

            for (int i = 0; i < config.Values.Count; i++)
            {
                object value = config.Values[i];

                string thisPath = path + (string.IsNullOrEmpty(path) ? string.Empty : ".") + i.ToString();

                if (value is ConfigObject child)
                {
                    writer.WriteLine(indent + "[" + level.ToString() + "] (" + thisPath + ") " + value.ToString());
                    WriteToFile(writer, child, level + 1, thisPath);
                }
                else if (value is string text)
                {
                    writer.WriteLine(indent + "[" + level.ToString() + "] (" + thisPath + ") \"" + text.ToString() + "\"");
                }
                else
                {
                    writer.WriteLine(indent + "[" + level.ToString() + "] (" + thisPath + ") " + value.ToString());
                }
            }
        }

        #region "UNDOCUMENTED FEATURE - WRITE IN EXTENDED FORMAT FOR TESTING"

        private string PathAsString(ConfigFileReader reader)
        {
            string path = string.Empty;

            for (int i = 0; i <= reader.Level; i++)
            {
                if (i > 0 && reader.Path[i] > -1)
                {
                    path += ".";
                }

                if (reader.Path[i] > -1)
                {
                    path += reader.Path[i].ToString();
                }
            }

            return path;
        }
        public string Format(ConfigFileReader reader, bool skipStartEnd = true, bool skipStartEndPath = true)
        {
            StringBuilder format = new StringBuilder();

            while (reader.Read())
            {
                if (reader.Token == TokenType.StartFile || reader.Token == TokenType.StartObject)
                {
                    if (!skipStartEnd)
                    {
                        if (reader.Level == 0)
                        {
                            format.AppendLine($"[+]{(skipStartEndPath ? string.Empty : $"({PathAsString(reader)})")}{reader.Char}");
                        }
                        else
                        {
                            format.AppendLine($"{"-".PadLeft(reader.Level * 3, '-')}[+]{(skipStartEndPath ? string.Empty : $"({PathAsString(reader)})")}{reader.Char}");
                        }
                    }
                }
                else if (reader.Token == TokenType.EndObject)
                {
                    if (!skipStartEnd)
                    {
                        format.AppendLine($"{"-".PadLeft((reader.Level + 1) * 3, '-')}[-]{(skipStartEndPath ? string.Empty : $"({PathAsString(reader)})")}{reader.Char}");
                    }
                }
                else if (reader.Token == TokenType.EndFile)
                {
                    if (!skipStartEnd)
                    {
                        format.AppendLine($"[-]{(skipStartEndPath ? string.Empty : $"({PathAsString(reader)})")}{reader.Char}");
                    }
                }
                else
                {
                    //string path = string.Empty;
                    //for (int i = 0; i <= Level; i++)
                    //{
                    //    if (i > 0) { path += "."; }
                    //    path += Path[i].ToString();
                    //}

                    string value = (reader.Value == null ? "null" : reader.Value);

                    if (reader.Level == 0)
                    {
                        format.AppendLine($"[{reader.Level}]({PathAsString(reader)}) {value}");
                    }
                    else
                    {
                        format.AppendLine($"{"-".PadLeft(reader.Level * 3, '-')}[{reader.Level}]({PathAsString(reader)}) {value}");
                    }
                }
            }

            return format.ToString();
        }

        #endregion
    }
}