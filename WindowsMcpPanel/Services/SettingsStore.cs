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
    internal static class SettingsStore
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "McpPanelWindows",
            "settings.json");

        public static string LoadConfigPath(string fallback)
        {
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    return fallback;
                }

                var settings = JsonTools.ParseObject(File.ReadAllText(SettingsPath, Encoding.UTF8));
                object value;
                if (settings.TryGetValue("configPath", out value) && value != null && !string.IsNullOrWhiteSpace(value.ToString()))
                {
                    return value.ToString();
                }
            }
            catch
            {
            }

            return fallback;
        }

        public static void SaveConfigPath(string path)
        {
            var directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                SettingsPath,
                JsonTools.PrettyPrint(new Dictionary<string, object> { { "configPath", path } }),
                new UTF8Encoding(false));
        }
    }
}

