using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace WindowsFactoryMcpPanel
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Any(a => string.Equals(a, "--self-test", StringComparison.OrdinalIgnoreCase)))
            {
                return SelfTest.Run();
            }

            if (args.Any(a => string.Equals(a, "--validate-factory", StringComparison.OrdinalIgnoreCase)))
            {
                return SelfTest.ValidateFactoryConfig();
            }

            var validateIndex = Array.FindIndex(args, a => string.Equals(a, "--validate", StringComparison.OrdinalIgnoreCase));
            if (validateIndex >= 0 && validateIndex + 1 < args.Length)
            {
                return SelfTest.ValidateConfig(args[validateIndex + 1]);
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            return 0;
        }
    }

    internal sealed class MainForm : Form
    {
        private static readonly string DefaultFactoryConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".factory",
            "mcp.json");

        private ConfigStore store;
        private string configPath;
        private readonly FlowLayoutPanel grid = new FlowLayoutPanel();
        private readonly TextBox addBox = new TextBox();
        private readonly Label statusLabel = new Label();
        private readonly Label countLabel = new Label();
        private readonly Label pathLabel = new Label();
        private readonly Button refreshButton = new Button();
        private readonly Button chooseButton = new Button();
        private readonly Button addButton = new Button();
        private readonly FileSystemWatcher watcher = new FileSystemWatcher();
        private readonly Timer statusTimer = new Timer();

        private bool suppressWatcher;
        private List<ServerEntry> servers = new List<ServerEntry>();

        public MainForm()
        {
            configPath = SettingsStore.LoadConfigPath(DefaultFactoryConfigPath);
            store = new ConfigStore(configPath);

            Text = "MCP Panel - Windows";
            MinimumSize = new Size(1080, 720);
            Size = new Size(1280, 820);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Palette.Black;
            ForeColor = Palette.Text;
            Font = new Font("Segoe UI", 10f);

            BuildLayout();
            ConfigureWatcher();
            LoadServers();
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Palette.Black,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(18),
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
            Controls.Add(root);

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                BackColor = Palette.Black,
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            root.Controls.Add(header, 0, 0);

            var titlePanel = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Black };
            var title = new Label
            {
                AutoSize = true,
                Text = "MCP Panel Windows",
                ForeColor = Palette.Text,
                Font = new Font("Segoe UI Semibold", 22f),
                Location = new Point(0, 2),
            };
            pathLabel.AutoSize = false;
            pathLabel.Text = configPath;
            pathLabel.ForeColor = Palette.Muted;
            pathLabel.Font = new Font("Consolas", 9f);
            pathLabel.Location = new Point(2, 45);
            pathLabel.Size = new Size(760, 20);
            titlePanel.Controls.Add(title);
            titlePanel.Controls.Add(pathLabel);
            header.Controls.Add(titlePanel, 0, 0);

            countLabel.Dock = DockStyle.Fill;
            countLabel.TextAlign = ContentAlignment.MiddleRight;
            countLabel.ForeColor = Palette.Muted;
            countLabel.Font = new Font("Segoe UI", 11f);
            header.Controls.Add(countLabel, 1, 0);

            refreshButton.Text = "Refresh";
            StyleButton(refreshButton, Palette.PanelAlt);
            refreshButton.Click += (sender, args) => LoadServers();
            header.Controls.Add(refreshButton, 2, 0);

            chooseButton.Text = "Choose";
            StyleButton(chooseButton, Palette.PanelAlt);
            chooseButton.Click += (sender, args) => ChooseConfigFile();
            header.Controls.Add(chooseButton, 3, 0);

            addButton.Text = "Add";
            StyleButton(addButton, Palette.Accent);
            addButton.Click += (sender, args) => AddServers();
            header.Controls.Add(addButton, 4, 0);

            grid.Dock = DockStyle.Fill;
            grid.AutoScroll = true;
            grid.WrapContents = true;
            grid.BackColor = Palette.Black;
            grid.Padding = new Padding(0, 8, 0, 8);
            root.Controls.Add(grid, 0, 1);

            var addPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                BackColor = Palette.Panel,
                Padding = new Padding(12),
                Margin = new Padding(0),
            };
            addPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            addPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(addPanel, 0, 2);

            var addHeader = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                BackColor = Palette.Panel,
            };
            addHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            addHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 520));
            addPanel.Controls.Add(addHeader, 0, 0);

            var addLabel = new Label
            {
                Text = "Paste a server, mcpServers wrapper, servers wrapper, or bare URL",
                Dock = DockStyle.Fill,
                ForeColor = Palette.Muted,
                Font = new Font("Segoe UI", 9f),
            };
            addHeader.Controls.Add(addLabel, 0, 0);

            statusLabel.Dock = DockStyle.Fill;
            statusLabel.TextAlign = ContentAlignment.MiddleRight;
            statusLabel.ForeColor = Palette.Muted;
            statusLabel.Font = new Font("Segoe UI", 9f);
            addHeader.Controls.Add(statusLabel, 1, 0);

            addBox.Dock = DockStyle.Fill;
            addBox.Multiline = true;
            addBox.ScrollBars = ScrollBars.Vertical;
            addBox.AcceptsReturn = true;
            addBox.AcceptsTab = true;
            addBox.BorderStyle = BorderStyle.FixedSingle;
            addBox.BackColor = Palette.Editor;
            addBox.ForeColor = Palette.Text;
            addBox.Font = new Font("Consolas", 10f);
            addBox.TextChanged += (sender, args) => PreviewAddValidation();
            addPanel.Controls.Add(addBox, 0, 1);

            statusTimer.Interval = 3600;
            statusTimer.Tick += (sender, args) =>
            {
                statusTimer.Stop();
                if (statusLabel.ForeColor != Palette.Error)
                {
                    statusLabel.Text = "";
                }
            };
        }

        private void ConfigureWatcher()
        {
            watcher.EnableRaisingEvents = false;
            watcher.Changed -= ConfigChangedExternally;
            watcher.Created -= ConfigChangedExternally;
            watcher.Renamed -= ConfigChangedExternally;

            var directory = Path.GetDirectoryName(configPath);
            if (directory == null)
            {
                return;
            }

            Directory.CreateDirectory(directory);
            watcher.Path = directory;
            watcher.Filter = Path.GetFileName(configPath);
            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size;
            watcher.Changed += ConfigChangedExternally;
            watcher.Created += ConfigChangedExternally;
            watcher.Renamed += ConfigChangedExternally;
            watcher.EnableRaisingEvents = true;
        }

        private void ConfigChangedExternally(object sender, FileSystemEventArgs args)
        {
            if (suppressWatcher)
            {
                return;
            }

            BeginInvoke((Action)(() =>
            {
                if (!suppressWatcher)
                {
                    LoadServers();
                    SetStatus("Reloaded external config change", Palette.Muted);
                }
            }));
        }

        private void LoadServers()
        {
            try
            {
                servers = store.ReadServers();
                RenderGrid();
                var enabledCount = servers.Count(server => server.Enabled);
                countLabel.Text = enabledCount + " on / " + servers.Count + " total";
                pathLabel.Text = configPath;
                SetStatus("Loaded " + servers.Count + " server(s)", Palette.Success);
            }
            catch (Exception ex)
            {
                SetStatus("Failed to load config: " + ex.Message, Palette.Error);
            }
        }

        private void RenderGrid()
        {
            grid.SuspendLayout();
            grid.Controls.Clear();

            foreach (var server in servers.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
            {
                grid.Controls.Add(new ServerCard(server, SaveServer, RenameServer, DeleteServer, ToggleServer));
            }

            if (servers.Count == 0)
            {
                var empty = new Label
                {
                    Text = "No Factory MCP servers yet. Add one below.",
                    ForeColor = Palette.Muted,
                    AutoSize = false,
                    Size = new Size(500, 80),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Font = new Font("Segoe UI", 12f),
                };
                grid.Controls.Add(empty);
            }

            grid.ResumeLayout();
        }

        private void PreviewAddValidation()
        {
            var text = addBox.Text.Trim();
            if (text.Length == 0)
            {
                SetStatus("", Palette.Muted);
                return;
            }

            Dictionary<string, Dictionary<string, object>> parsed;
            if (!ServerExtractor.TryExtract(text, out parsed))
            {
                SetStatus("Waiting for valid JSON/server input", Palette.Error);
                return;
            }

            var invalid = parsed
                .Where(pair => !ServerValidator.IsValid(pair.Value))
                .Select(pair => pair.Key + ": " + ServerValidator.InvalidReason(pair.Value))
                .ToList();

            if (invalid.Count > 0)
            {
                SetStatus("Invalid: " + string.Join("; ", invalid), Palette.Error);
            }
            else
            {
                SetStatus("Valid. Found " + parsed.Count + " server(s).", Palette.Success);
            }
        }

        private void AddServers()
        {
            Dictionary<string, Dictionary<string, object>> parsed;
            if (!ServerExtractor.TryExtract(addBox.Text, out parsed))
            {
                SetStatus("Could not parse server input", Palette.Error);
                return;
            }

            var invalid = parsed
                .Where(pair => !ServerValidator.IsValid(pair.Value))
                .Select(pair => pair.Key + ": " + ServerValidator.InvalidReason(pair.Value))
                .ToList();

            if (invalid.Count > 0)
            {
                SetStatus("Invalid server config: " + string.Join("; ", invalid), Palette.Error);
                return;
            }

            foreach (var pair in parsed)
            {
                var existing = servers.FirstOrDefault(s => string.Equals(s.Name, pair.Key, StringComparison.Ordinal));
                pair.Value["disabled"] = false;

                if (existing == null)
                {
                    servers.Add(new ServerEntry(pair.Key, pair.Value));
                }
                else
                {
                    existing.Config = pair.Value;
                    existing.UpdatedAt = DateTime.Now;
                }
            }

            SaveAll("Added " + parsed.Count + " server(s)");
            addBox.Clear();
        }

        private void SaveServer(ServerEntry server, string name, string json)
        {
            try
            {
                var parsed = JsonTools.ParseObject(json);
                if (!ServerValidator.IsValid(parsed))
                {
                    SetStatus(name + " invalid: " + ServerValidator.InvalidReason(parsed), Palette.Error);
                    return;
                }

                server.Config = parsed;
                server.UpdatedAt = DateTime.Now;
                SaveAll("Saved " + name);
            }
            catch (Exception ex)
            {
                SetStatus("Failed to save " + name + ": " + ex.Message, Palette.Error);
            }
        }

        private void RenameServer(ServerEntry server, string newName)
        {
            var trimmed = (newName ?? "").Trim();
            if (trimmed.Length == 0)
            {
                SetStatus("Server name cannot be empty", Palette.Error);
                return;
            }

            if (servers.Any(s => !ReferenceEquals(s, server) && string.Equals(s.Name, trimmed, StringComparison.Ordinal)))
            {
                SetStatus("A server named '" + trimmed + "' already exists", Palette.Error);
                return;
            }

            server.Name = trimmed;
            server.UpdatedAt = DateTime.Now;
            SaveAll("Renamed server");
        }

        private void DeleteServer(ServerEntry server)
        {
            var answer = MessageBox.Show(
                "Delete '" + server.Name + "' from this MCP config?",
                "Delete MCP server",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes)
            {
                return;
            }

            servers.Remove(server);
            SaveAll("Deleted " + server.Name);
        }

        private void ToggleServer(ServerEntry server, bool enabled)
        {
            server.Enabled = enabled;
            server.UpdatedAt = DateTime.Now;
            SaveAll(server.Name + " " + (enabled ? "enabled" : "disabled"));
        }

        private void SaveAll(string message)
        {
            try
            {
                suppressWatcher = true;
                store.WriteServers(servers);
                RenderGrid();
                var enabledCount = servers.Count(server => server.Enabled);
                countLabel.Text = enabledCount + " on / " + servers.Count + " total";
                SetStatus(message, Palette.Success);
            }
            catch (Exception ex)
            {
                SetStatus("Failed to write Factory config: " + ex.Message, Palette.Error);
            }
            finally
            {
                var timer = new Timer { Interval = 700 };
                timer.Tick += (sender, args) =>
                {
                    timer.Stop();
                    timer.Dispose();
                    suppressWatcher = false;
                };
                timer.Start();
            }
        }

        private void ChooseConfigFile()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Choose MCP JSON config";
                dialog.Filter = "MCP JSON files (*.json)|*.json|All files (*.*)|*.*";
                dialog.CheckFileExists = false;
                dialog.FileName = Path.GetFileName(configPath);
                var directory = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                {
                    dialog.InitialDirectory = directory;
                }

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                configPath = dialog.FileName;
                store = new ConfigStore(configPath);
                SettingsStore.SaveConfigPath(configPath);
                ConfigureWatcher();
                LoadServers();
                SetStatus("Using " + configPath, Palette.Success);
            }
        }

        private void SetStatus(string message, Color color)
        {
            statusTimer.Stop();
            statusLabel.Text = message;
            statusLabel.ForeColor = color;
            if (message.Length > 0)
            {
                statusTimer.Start();
            }
        }

        private static void StyleButton(Button button, Color color)
        {
            button.Dock = DockStyle.Fill;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = Palette.Border;
            button.BackColor = color;
            button.ForeColor = Palette.Text;
            button.Font = new Font("Segoe UI Semibold", 10f);
            button.Margin = new Padding(8, 14, 0, 14);
            button.Cursor = Cursors.Hand;
        }
    }

    internal sealed class ServerCard : Panel
    {
        private readonly ServerEntry server;
        private readonly Action<ServerEntry, string, string> save;
        private readonly Action<ServerEntry, string> rename;
        private readonly Action<ServerEntry> delete;
        private readonly Action<ServerEntry, bool> toggle;

        private readonly TextBox nameBox = new TextBox();
        private readonly TextBox jsonBox = new TextBox();
        private readonly CheckBox enabledBox = new CheckBox();

        public ServerCard(
            ServerEntry server,
            Action<ServerEntry, string, string> save,
            Action<ServerEntry, string> rename,
            Action<ServerEntry> delete,
            Action<ServerEntry, bool> toggle)
        {
            this.server = server;
            this.save = save;
            this.rename = rename;
            this.delete = delete;
            this.toggle = toggle;

            Size = new Size(382, 318);
            Margin = new Padding(0, 0, 14, 14);
            BackColor = Palette.Panel;
            ForeColor = Palette.Text;
            Padding = new Padding(12);
            Build();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(server.Enabled ? Palette.Accent : Palette.Border, 1))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }

        private void Build()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 5,
                ColumnCount = 1,
                BackColor = Palette.Panel,
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            Controls.Add(layout);

            var top = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                BackColor = Palette.Panel,
            };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
            layout.Controls.Add(top, 0, 0);

            nameBox.Text = server.Name;
            nameBox.Dock = DockStyle.Fill;
            nameBox.BackColor = Palette.Editor;
            nameBox.ForeColor = Palette.Text;
            nameBox.BorderStyle = BorderStyle.FixedSingle;
            nameBox.Font = new Font("Segoe UI Semibold", 11f);
            nameBox.Leave += (sender, args) =>
            {
                if (!string.Equals(nameBox.Text.Trim(), server.Name, StringComparison.Ordinal))
                {
                    rename(server, nameBox.Text);
                }
            };
            top.Controls.Add(nameBox, 0, 0);

            enabledBox.Text = server.Enabled ? "On" : "Off";
            enabledBox.Checked = server.Enabled;
            enabledBox.Appearance = Appearance.Button;
            enabledBox.Dock = DockStyle.Fill;
            enabledBox.TextAlign = ContentAlignment.MiddleCenter;
            enabledBox.FlatStyle = FlatStyle.Flat;
            enabledBox.FlatAppearance.BorderColor = server.Enabled ? Palette.Accent : Palette.Border;
            enabledBox.BackColor = server.Enabled ? Palette.Accent : Palette.PanelAlt;
            enabledBox.ForeColor = Palette.Text;
            enabledBox.Font = new Font("Segoe UI Semibold", 10f);
            enabledBox.CheckedChanged += (sender, args) =>
            {
                enabledBox.Text = enabledBox.Checked ? "On" : "Off";
                toggle(server, enabledBox.Checked);
            };
            top.Controls.Add(enabledBox, 1, 0);

            var summary = new Label
            {
                Dock = DockStyle.Fill,
                Text = ServerValidator.Summary(server.Config),
                ForeColor = Palette.Muted,
                Font = new Font("Segoe UI", 9f),
                AutoEllipsis = true,
            };
            layout.Controls.Add(summary, 0, 1);

            jsonBox.Text = JsonTools.PrettyPrint(server.Config);
            jsonBox.Dock = DockStyle.Fill;
            jsonBox.Multiline = true;
            jsonBox.ScrollBars = ScrollBars.Vertical;
            jsonBox.AcceptsReturn = true;
            jsonBox.AcceptsTab = true;
            jsonBox.BorderStyle = BorderStyle.FixedSingle;
            jsonBox.BackColor = Palette.Editor;
            jsonBox.ForeColor = Palette.Text;
            jsonBox.Font = new Font("Consolas", 9.5f);
            layout.Controls.Add(jsonBox, 0, 2);

            var buttons = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                BackColor = Palette.Panel,
            };
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            layout.Controls.Add(buttons, 0, 3);

            var saveButton = MakeButton("Save", Palette.Accent);
            saveButton.Click += (sender, args) => save(server, server.Name, jsonBox.Text);
            buttons.Controls.Add(saveButton, 0, 0);

            var resetButton = MakeButton("Reset", Palette.PanelAlt);
            resetButton.Click += (sender, args) => jsonBox.Text = JsonTools.PrettyPrint(server.Config);
            buttons.Controls.Add(resetButton, 1, 0);

            var deleteButton = MakeButton("Delete", Palette.Danger);
            deleteButton.Click += (sender, args) => delete(server);
            buttons.Controls.Add(deleteButton, 2, 0);

            var valid = ServerValidator.IsValid(server.Config);
            var footer = new Label
            {
                Dock = DockStyle.Fill,
                Text = valid ? "Valid MCP config" : "Invalid: " + ServerValidator.InvalidReason(server.Config),
                ForeColor = valid ? Palette.Success : Palette.Error,
                Font = new Font("Segoe UI", 8.5f),
                AutoEllipsis = true,
            };
            layout.Controls.Add(footer, 0, 4);
        }

        private static Button MakeButton(string text, Color color)
        {
            var button = new Button
            {
                Text = text,
                Dock = DockStyle.Fill,
                BackColor = color,
                ForeColor = Palette.Text,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9f),
                Margin = new Padding(0, 7, 7, 4),
                Cursor = Cursors.Hand,
            };
            button.FlatAppearance.BorderColor = Palette.Border;
            button.FlatAppearance.BorderSize = 1;
            return button;
        }
    }

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

    internal sealed class ServerEntry
    {
        public ServerEntry(string name, Dictionary<string, object> config)
        {
            Name = name;
            Config = config;
            UpdatedAt = DateTime.Now;
        }

        public string Name { get; set; }

        public Dictionary<string, object> Config { get; set; }

        public DateTime UpdatedAt { get; set; }

        public bool Enabled
        {
            get
            {
                object value;
                return !Config.TryGetValue("disabled", out value) || !JsonTools.IsTruthy(value);
            }

            set
            {
                Config["disabled"] = !value;
            }
        }
    }

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

    internal static class JsonTools
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public static Dictionary<string, object> ParseObject(string json)
        {
            var parsed = Serializer.DeserializeObject(json) as Dictionary<string, object>;
            if (parsed == null)
            {
                throw new InvalidOperationException("JSON root must be an object");
            }

            return parsed;
        }

        public static Dictionary<string, object> AsObject(object value)
        {
            return value as Dictionary<string, object>;
        }

        public static Dictionary<string, object> StringDictionary(object value)
        {
            var dictionary = value as Dictionary<string, object>;
            if (dictionary == null)
            {
                return null;
            }

            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var pair in dictionary)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                result[pair.Key] = pair.Value.ToString();
            }

            return result.Count == 0 ? null : result;
        }

        public static bool IsTruthy(object value)
        {
            if (value is bool)
            {
                return (bool)value;
            }

            var text = value == null ? "" : value.ToString();
            bool parsed;
            return bool.TryParse(text, out parsed) && parsed;
        }

        public static string PrettyPrint(object value)
        {
            var builder = new StringBuilder();
            WriteValue(builder, value, 0);
            builder.AppendLine();
            return builder.ToString();
        }

        private static void WriteValue(StringBuilder builder, object value, int indent)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            var dictionary = value as Dictionary<string, object>;
            if (dictionary != null)
            {
                WriteObject(builder, dictionary, indent);
                return;
            }

            var array = value as ArrayList;
            if (array != null)
            {
                WriteArray(builder, array, indent);
                return;
            }

            if (value is string)
            {
                builder.Append(Serializer.Serialize(value));
                return;
            }

            if (value is bool)
            {
                builder.Append((bool)value ? "true" : "false");
                return;
            }

            if (value is int || value is long || value is decimal || value is double || value is float)
            {
                builder.Append(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
                return;
            }

            builder.Append(Serializer.Serialize(value));
        }

        private static void WriteObject(StringBuilder builder, Dictionary<string, object> dictionary, int indent)
        {
            builder.AppendLine("{");
            var entries = dictionary.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToList();
            for (var i = 0; i < entries.Count; i++)
            {
                Indent(builder, indent + 1);
                builder.Append(Serializer.Serialize(entries[i].Key));
                builder.Append(": ");
                WriteValue(builder, entries[i].Value, indent + 1);
                if (i < entries.Count - 1)
                {
                    builder.Append(",");
                }

                builder.AppendLine();
            }

            Indent(builder, indent);
            builder.Append("}");
        }

        private static void WriteArray(StringBuilder builder, ArrayList array, int indent)
        {
            if (array.Count == 0)
            {
                builder.Append("[]");
                return;
            }

            builder.AppendLine("[");
            for (var i = 0; i < array.Count; i++)
            {
                Indent(builder, indent + 1);
                WriteValue(builder, array[i], indent + 1);
                if (i < array.Count - 1)
                {
                    builder.Append(",");
                }

                builder.AppendLine();
            }

            Indent(builder, indent);
            builder.Append("]");
        }

        private static void Indent(StringBuilder builder, int indent)
        {
            builder.Append(new string(' ', indent * 2));
        }
    }

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

    internal static class Palette
    {
        public static readonly Color Black = Color.FromArgb(0, 0, 0);
        public static readonly Color Panel = Color.FromArgb(8, 10, 12);
        public static readonly Color PanelAlt = Color.FromArgb(20, 24, 28);
        public static readonly Color Editor = Color.FromArgb(2, 4, 6);
        public static readonly Color Border = Color.FromArgb(41, 50, 58);
        public static readonly Color Text = Color.FromArgb(238, 246, 249);
        public static readonly Color Muted = Color.FromArgb(125, 142, 150);
        public static readonly Color Accent = Color.FromArgb(0, 188, 212);
        public static readonly Color Success = Color.FromArgb(75, 220, 146);
        public static readonly Color Error = Color.FromArgb(255, 96, 115);
        public static readonly Color Danger = Color.FromArgb(82, 23, 32);
    }
}
