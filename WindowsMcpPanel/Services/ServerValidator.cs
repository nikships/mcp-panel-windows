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
    internal static class ServerValidator
    {
        public static bool IsValid(Dictionary<string, object> config)
        {
            var type = StringValue(config, "type");
            if (type == "stdio" && IsNonEmpty(config, "command"))
            {
                return true;
            }

            if ((type == "http" || type == "sse") && IsNonEmpty(config, "url"))
            {
                return true;
            }

            if (IsNonEmpty(config, "httpUrl"))
            {
                return true;
            }

            if (IsNonEmpty(config, "url"))
            {
                return true;
            }

            return IsNonEmpty(config, "command") || config.ContainsKey("transport") || HasNonEmptyArray(config, "remotes");
        }

        public static string InvalidReason(Dictionary<string, object> config)
        {
            if (!config.ContainsKey("command") && !config.ContainsKey("httpUrl") && !config.ContainsKey("transport") && !config.ContainsKey("remotes") && !config.ContainsKey("url"))
            {
                return "missing command, httpUrl, url, transport, or remotes";
            }

            if (config.ContainsKey("command") && !IsNonEmpty(config, "command"))
            {
                return "empty command";
            }

            if (config.ContainsKey("httpUrl") && !IsNonEmpty(config, "httpUrl"))
            {
                return "empty httpUrl";
            }

            if (config.ContainsKey("url") && !IsNonEmpty(config, "url"))
            {
                return "empty url";
            }

            return "unknown issue";
        }

        public static string Summary(Dictionary<string, object> config)
        {
            var type = StringValue(config, "type");
            var url = StringValue(config, "url");
            if ((type == "http" || type == "sse") && !string.IsNullOrWhiteSpace(url))
            {
                return type.ToUpperInvariant() + " -> " + Host(url);
            }

            var command = StringValue(config, "command");
            if (!string.IsNullOrWhiteSpace(command))
            {
                return command.Trim();
            }

            var httpUrl = StringValue(config, "httpUrl");
            if (!string.IsNullOrWhiteSpace(httpUrl))
            {
                return "HTTP -> " + Host(httpUrl);
            }

            object transport;
            if (config.TryGetValue("transport", out transport))
            {
                var transportObject = JsonTools.AsObject(transport);
                if (transportObject != null)
                {
                    var transportType = StringValue(transportObject, "type") ?? "custom";
                    var transportUrl = StringValue(transportObject, "url");
                    return "Remote " + transportType + " -> " + (string.IsNullOrWhiteSpace(transportUrl) ? "custom endpoint" : Host(transportUrl));
                }
            }

            object remotes;
            if (config.TryGetValue("remotes", out remotes))
            {
                var remoteArray = remotes as ArrayList;
                if (remoteArray != null && remoteArray.Count > 0)
                {
                    var first = JsonTools.AsObject(remoteArray[0]);
                    if (first != null)
                    {
                        return "Remote " + (StringValue(first, "type") ?? "custom") + " -> " + Host(StringValue(first, "url"));
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(url))
            {
                return "Remote -> " + Host(url);
            }

            return "Custom server configuration";
        }

        private static string StringValue(Dictionary<string, object> config, string key)
        {
            object value;
            return config.TryGetValue(key, out value) && value != null ? value.ToString() : null;
        }

        private static bool IsNonEmpty(Dictionary<string, object> config, string key)
        {
            var value = StringValue(config, key);
            return !string.IsNullOrWhiteSpace(value);
        }

        private static bool HasNonEmptyArray(Dictionary<string, object> config, string key)
        {
            object value;
            if (!config.TryGetValue(key, out value))
            {
                return false;
            }

            var array = value as ArrayList;
            return array != null && array.Count > 0;
        }

        private static string Host(string url)
        {
            Uri uri;
            return Uri.TryCreate(url, UriKind.Absolute, out uri) && !string.IsNullOrWhiteSpace(uri.Host)
                ? uri.Host
                : url;
        }
    }
}

