using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace WindowsMcpPanel
{
    internal static class ServerExtractor
    {
        public static bool TryExtract(string raw, out Dictionary<string, Dictionary<string, object>> servers)
        {
            servers = null;
            var normalized = (raw ?? "").Trim();
            if (normalized.Length == 0)
            {
                return false;
            }

            var urlEntry = TryCreateUrlEntry(normalized);
            if (urlEntry != null)
            {
                servers = urlEntry;
                return true;
            }

            normalized = NormalizeQuotes(normalized);
            if (!normalized.StartsWith("{", StringComparison.Ordinal))
            {
                normalized = "{" + normalized + "}";
            }

            normalized = Regex.Replace(normalized, @",\s*([}\]])", "$1");

            Dictionary<string, object> parsed;
            try
            {
                parsed = JsonTools.ParseObject(normalized);
            }
            catch
            {
                return false;
            }

            object wrapped;
            if (parsed.TryGetValue("mcpServers", out wrapped) || parsed.TryGetValue("servers", out wrapped))
            {
                parsed = wrapped as Dictionary<string, object>;
                if (parsed == null)
                {
                    return false;
                }
            }

            var result = new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);
            foreach (var pair in parsed)
            {
                var config = NormalizeServerValue(pair.Value);
                if (config != null)
                {
                    result[pair.Key] = config;
                }
            }

            if (result.Count == 0)
            {
                return false;
            }

            servers = result;
            return true;
        }

        private static Dictionary<string, Dictionary<string, object>> TryCreateUrlEntry(string input)
        {
            if (input.Contains("{") || input.Contains("\"") || Regex.IsMatch(input, @"\s"))
            {
                return null;
            }

            var hasScheme = input.Contains("://");
            if (!hasScheme && !input.Contains("."))
            {
                return null;
            }

            var url = hasScheme ? input : "https://" + input;
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
            {
                return null;
            }

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                return null;
            }

            var parts = uri.Host.Split('.');
            var name = parts.Length >= 2 ? parts[parts.Length - 2] : uri.Host;
            return new Dictionary<string, Dictionary<string, object>>
            {
                {
                    name,
                    new Dictionary<string, object>
                    {
                        { "type", "http" },
                        { "url", url },
                    }
                },
            };
        }

        private static Dictionary<string, object> NormalizeServerValue(object value)
        {
            var config = JsonTools.AsObject(value);
            if (config == null)
            {
                return null;
            }

            object command;
            if (config.TryGetValue("command", out command))
            {
                var commandArray = command as ArrayList;
                if (commandArray != null)
                {
                    var parts = commandArray.Cast<object>().Select(item => item == null ? null : item.ToString()).Where(item => item != null).ToList();
                    if (parts.Count > 0)
                    {
                        config["command"] = parts[0];
                        if (!config.ContainsKey("args") && parts.Count > 1)
                        {
                            config["args"] = new ArrayList(parts.Skip(1).Cast<object>().ToList());
                        }
                    }
                }
            }

            object environment;
            if (!config.ContainsKey("env") && config.TryGetValue("environment", out environment))
            {
                var env = JsonTools.StringDictionary(environment);
                if (env != null)
                {
                    config["env"] = env;
                }
            }

            object headers;
            if (config.TryGetValue("headers", out headers))
            {
                var stringHeaders = JsonTools.StringDictionary(headers);
                if (stringHeaders != null)
                {
                    config["headers"] = stringHeaders;
                }
            }

            object transport;
            if (config.TryGetValue("transport", out transport))
            {
                var transportObject = JsonTools.AsObject(transport);
                if (transportObject != null)
                {
                    object transportHeaders;
                    if (transportObject.TryGetValue("headers", out transportHeaders))
                    {
                        var stringHeaders = JsonTools.StringDictionary(transportHeaders);
                        if (stringHeaders != null)
                        {
                            transportObject["headers"] = stringHeaders;
                        }
                    }

                    config["transport"] = transportObject;
                }
            }

            object remotes;
            if (config.TryGetValue("remotes", out remotes))
            {
                var remoteArray = remotes as ArrayList;
                if (remoteArray != null)
                {
                    foreach (var remote in remoteArray)
                    {
                        var remoteObject = JsonTools.AsObject(remote);
                        if (remoteObject == null)
                        {
                            continue;
                        }

                        object remoteHeaders;
                        if (remoteObject.TryGetValue("headers", out remoteHeaders))
                        {
                            var stringHeaders = JsonTools.StringDictionary(remoteHeaders);
                            if (stringHeaders != null)
                            {
                                remoteObject["headers"] = stringHeaders;
                            }
                        }
                    }
                }
            }

            return config;
        }

        private static string NormalizeQuotes(string value)
        {
            return value
                .Replace('\u201c', '"')
                .Replace('\u201d', '"')
                .Replace('\u2018', '\'')
                .Replace('\u2019', '\'');
        }
    }
}

