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
}

