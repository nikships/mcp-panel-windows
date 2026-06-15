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
}

