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
}

