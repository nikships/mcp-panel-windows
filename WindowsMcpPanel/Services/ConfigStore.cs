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
    internal sealed class ConfigStore
    {
        private readonly string path;
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public ConfigStore(string path)
        {
            this.path = path;
        }

        public List<ServerEntry> ReadServers()
        {
            if (!File.Exists(path))
            {
                return new List<ServerEntry>();
            }

            var root = JsonTools.ParseObject(File.ReadAllText(path, Encoding.UTF8));
            object serversObject;
            if (!root.TryGetValue("mcpServers", out serversObject))
            {
                return new List<ServerEntry>();
            }

            var serversDictionary = serversObject as Dictionary<string, object>;
            if (serversDictionary == null)
            {
                return new List<ServerEntry>();
            }

            return serversDictionary
                .Select(pair => new ServerEntry(pair.Key, JsonTools.AsObject(pair.Value)))
                .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public void WriteServers(IEnumerable<ServerEntry> entries)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var root = File.Exists(path)
                ? JsonTools.ParseObject(File.ReadAllText(path, Encoding.UTF8))
                : new Dictionary<string, object>();

            var serverMap = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var entry in entries.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
            {
                serverMap[entry.Name] = entry.Config;
            }

            root["mcpServers"] = serverMap;

            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonTools.PrettyPrint(root), new UTF8Encoding(false));
            if (File.Exists(path))
            {
                File.Replace(temp, path, null);
            }
            else
            {
                File.Move(temp, path);
            }
        }
    }
}

