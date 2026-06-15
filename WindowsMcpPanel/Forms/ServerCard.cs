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
}

