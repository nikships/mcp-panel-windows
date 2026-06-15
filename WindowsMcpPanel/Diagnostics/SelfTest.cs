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
    internal static class SelfTest
    {
        private static readonly string FactoryConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".factory",
            "mcp.json");

        public static int Run()
        {
            var failures = new List<string>();
            AssertExtracts(failures, "https://huggingface.co/mcp", "huggingface", "url");
            AssertExtracts(failures, "\"x\": { \"command\": [\"uvx\", \"pkg\"], \"environment\": { \"A\": 1, }, }", "x", "command");
            AssertInvalid(failures, "{\"bad\": {\"command\": \"   \"}}");
            AssertValid(failures, "{\"mcpServers\":{\"http\":{\"type\":\"http\",\"url\":\"https://example.com/mcp\"}}}");

            if (failures.Count > 0)
            {
                Console.Error.WriteLine(string.Join(Environment.NewLine, failures));
                return 1;
            }

            Console.WriteLine("Self-test passed");
            return 0;
        }

        public static int ValidateFactoryConfig()
        {
            return ValidateConfig(FactoryConfigPath);
        }

        public static int ValidateConfig(string path)
        {
            try
            {
                var store = new ConfigStore(path);
                var servers = store.ReadServers();
                var invalid = servers
                    .Where(server => !ServerValidator.IsValid(server.Config))
                    .Select(server => server.Name + ": " + ServerValidator.InvalidReason(server.Config))
                    .ToList();

                if (invalid.Count > 0)
                {
                    Console.Error.WriteLine("Invalid MCP server(s):");
                    Console.Error.WriteLine(string.Join(Environment.NewLine, invalid));
                    return 1;
                }

                Console.WriteLine("MCP config valid: " + servers.Count + " server(s), " + servers.Count(server => server.Enabled) + " enabled at " + path);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("MCP config validation failed: " + ex.Message);
                return 1;
            }
        }

        private static void AssertExtracts(List<string> failures, string input, string name, string key)
        {
            Dictionary<string, Dictionary<string, object>> parsed;
            if (!ServerExtractor.TryExtract(input, out parsed) || !parsed.ContainsKey(name) || !parsed[name].ContainsKey(key))
            {
                failures.Add("Expected " + input + " to extract " + name + "." + key);
            }
        }

        private static void AssertInvalid(List<string> failures, string input)
        {
            Dictionary<string, Dictionary<string, object>> parsed;
            if (!ServerExtractor.TryExtract(input, out parsed) || parsed.Values.All(ServerValidator.IsValid))
            {
                failures.Add("Expected invalid config: " + input);
            }
        }

        private static void AssertValid(List<string> failures, string input)
        {
            Dictionary<string, Dictionary<string, object>> parsed;
            if (!ServerExtractor.TryExtract(input, out parsed) || parsed.Values.Any(config => !ServerValidator.IsValid(config)))
            {
                failures.Add("Expected valid config: " + input);
            }
        }
    }
}

