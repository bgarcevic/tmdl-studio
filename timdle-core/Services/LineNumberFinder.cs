using System;
using System.IO;
using System.Linq;

namespace TmdlStudio.Services
{
    public static class LineNumberFinder
    {
        public static int? FindLineNumber(string filePath, string itemName, string itemType)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var content = File.ReadAllText(filePath);
            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.None);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                bool found = itemType switch
                {
                    "column" => IsColumnDeclaration(line, itemName),
                    "measure" => IsMeasureDeclaration(line, itemName),
                    "partition" => IsPartitionDeclaration(line, itemName),
                    "relationship" => IsRelationshipDeclaration(line, itemName),
                    "expression" => IsExpressionDeclaration(line, itemName),
                    "cultureInfo" => IsCultureDeclaration(line, itemName),
                    _ => false
                };

                if (found)
                {
                    return i + 1;
                }
            }

            return null;
        }

        private static bool IsColumnDeclaration(string line, string name)
        {
            string pattern = NeedsQuotes(name) 
                ? $"column '{EscapeSingleQuotes(name)}'"
                : $"column {name}";
            return line.StartsWith(pattern);
        }

        private static bool IsMeasureDeclaration(string line, string name)
        {
            string pattern = NeedsQuotes(name)
                ? $"measure '{EscapeSingleQuotes(name)}'"
                : $"measure {name}";
            return line.StartsWith(pattern);
        }

        private static bool IsPartitionDeclaration(string line, string name)
        {
            string pattern = NeedsQuotes(name)
                ? $"partition '{EscapeSingleQuotes(name)}'"
                : $"partition {name}";
            return line.StartsWith(pattern);
        }

        private static bool IsRelationshipDeclaration(string line, string name)
        {
            return line.StartsWith($"relationship {name}");
        }

        private static bool IsExpressionDeclaration(string line, string name)
        {
            string pattern = NeedsQuotes(name)
                ? $"expression '{EscapeSingleQuotes(name)}'"
                : $"expression {name}";
            return line.StartsWith(pattern);
        }

        private static bool IsCultureDeclaration(string line, string name)
        {
            return line.StartsWith($"cultureInfo {name}");
        }

        private static bool NeedsQuotes(string name)
        {
            char[] specialChars = new[] { ' ', '=', ':', '\'' };
            return name.Any(c => Array.Exists(specialChars, sc => sc == c));
        }

        private static string EscapeSingleQuotes(string name)
        {
            return name.Replace("'", "''");
        }
    }
}
