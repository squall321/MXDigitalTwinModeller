using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.IO
{
    /// <summary>
    /// Simple YAML writer for metadata export
    /// Supports basic key-value pairs and lists
    /// </summary>
    public class YamlWriter
    {
        private readonly StringBuilder sb = new StringBuilder();
        private int indentLevel = 0;
        private const string IndentString = "  ";

        public YamlWriter()
        {
        }

        /// <summary>
        /// Add a comment line
        /// </summary>
        public void WriteComment(string comment)
        {
            sb.AppendLine($"# {comment}");
        }

        /// <summary>
        /// Add a blank line
        /// </summary>
        public void WriteLine()
        {
            sb.AppendLine();
        }

        /// <summary>
        /// Write a key-value pair
        /// </summary>
        public void WriteKeyValue(string key, object value)
        {
            string indent = new string(' ', indentLevel * 2);

            if (value == null)
            {
                sb.AppendLine($"{indent}{key}: null");
            }
            else if (value is string strValue)
            {
                // Escape special characters if needed
                if (strValue.Contains(":") || strValue.Contains("#") || strValue.Contains("\""))
                {
                    strValue = $"\"{strValue.Replace("\"", "\\\"")}\"";
                }
                sb.AppendLine($"{indent}{key}: {strValue}");
            }
            else if (value is bool boolValue)
            {
                sb.AppendLine($"{indent}{key}: {boolValue.ToString().ToLower()}");
            }
            else if (value is DateTime dateValue)
            {
                sb.AppendLine($"{indent}{key}: {dateValue:yyyy-MM-ddTHH:mm:ssZ}");
            }
            else if (value is double || value is float)
            {
                sb.AppendLine($"{indent}{key}: {value:F6}");
            }
            else
            {
                sb.AppendLine($"{indent}{key}: {value}");
            }
        }

        /// <summary>
        /// Start a list (array)
        /// </summary>
        public void WriteListStart(string key)
        {
            string indent = new string(' ', indentLevel * 2);
            sb.AppendLine($"{indent}{key}:");
            indentLevel++;
        }

        /// <summary>
        /// Write a list item
        /// </summary>
        public void WriteListItem(object value)
        {
            string indent = new string(' ', indentLevel * 2);

            if (value is string strValue)
            {
                if (strValue.Contains(":") || strValue.Contains("#"))
                {
                    strValue = $"\"{strValue}\"";
                }
                sb.AppendLine($"{indent}- {strValue}");
            }
            else
            {
                sb.AppendLine($"{indent}- {value}");
            }
        }

        /// <summary>
        /// End a list
        /// </summary>
        public void WriteListEnd()
        {
            if (indentLevel > 0)
                indentLevel--;
        }

        /// <summary>
        /// Start a nested object
        /// </summary>
        public void WriteObjectStart(string key)
        {
            string indent = new string(' ', indentLevel * 2);
            sb.AppendLine($"{indent}{key}:");
            indentLevel++;
        }

        /// <summary>
        /// End a nested object
        /// </summary>
        public void WriteObjectEnd()
        {
            if (indentLevel > 0)
                indentLevel--;
        }

        /// <summary>
        /// Get the final YAML string
        /// </summary>
        public override string ToString()
        {
            return sb.ToString();
        }

        /// <summary>
        /// Save to file
        /// </summary>
        public void SaveToFile(string filePath)
        {
            File.WriteAllText(filePath, ToString(), Encoding.UTF8);
        }
    }
}
