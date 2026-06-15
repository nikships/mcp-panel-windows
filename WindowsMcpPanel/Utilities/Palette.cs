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
