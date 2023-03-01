using System;
using System.Collections.Generic;
using System.Text;
using System.Web;

namespace DaJet
{
    public static class UriHelper
    {
        public static Dictionary<string, string> CreateOptions(in Uri uri)
        {
            Dictionary<string, string> options = new();

            string host = uri.Host;
            if (uri.Port > 0) { host += $":{uri.Port}"; }

            options.Add("host", host);

            string[] info = uri.UserInfo.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (info is not null && info.Length == 2)
            {
                options.Add("user", HttpUtility.UrlDecode(info[0], Encoding.UTF8));
                options.Add("pswd", HttpUtility.UrlDecode(info[1], Encoding.UTF8));
            }

            if (uri.Segments is not null && uri.Segments.Length > 0)
            {
                options.Add("dbname", HttpUtility.UrlDecode(uri.Segments[0].TrimEnd('/'), Encoding.UTF8));
            }

            if (!string.IsNullOrWhiteSpace(uri.Query))
            {
                string[] query = uri.Query.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                
                if (query is not null && query.Length > 0)
                {
                    foreach (string parameter in query)
                    {
                        string[] key_value = parameter.Split('=', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                        if (key_value is not null && key_value.Length > 0)
                        {
                            string key = HttpUtility.UrlDecode(key_value[0], Encoding.UTF8);

                            if (key_value.Length > 1)
                            {
                                string value = HttpUtility.UrlDecode(key_value[1], Encoding.UTF8);

                                options.Add(key, value);
                            }
                            else
                            {
                                options.Add(key, string.Empty);
                            }
                        }
                    }
                }
            }

            return options;
        }
    }
}