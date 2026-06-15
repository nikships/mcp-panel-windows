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
}

