using System;
using System.Collections.Generic;

namespace TmdlStudio.Services
{
    /// <summary>
    /// Console input helpers for interactive CLI prompts.
    /// </summary>
    public static class ConsolePrompter
    {
        /// <summary>
        /// Prompts for required text input.
        /// </summary>
        public static string PromptRequired(string label, string defaultValue = null)
        {
            while (true)
            {
                if (!string.IsNullOrWhiteSpace(defaultValue))
                {
                    Console.Write($"{label} [{defaultValue}]: ");
                }
                else
                {
                    Console.Write($"{label}: ");
                }

                var value = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(value))
                {
                    value = defaultValue;
                }

                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }

                Console.WriteLine("This value is required.");
            }
        }

        /// <summary>
        /// Prompts for secret input with masked characters.
        /// </summary>
        public static string PromptSecret(string label)
        {
            while (true)
            {
                Console.Write($"{label}: ");
                var chars = new List<char>();

                while (true)
                {
                    var key = Console.ReadKey(intercept: true);

                    if (key.Key == ConsoleKey.Enter)
                    {
                        Console.WriteLine();
                        break;
                    }

                    if (key.Key == ConsoleKey.Backspace)
                    {
                        if (chars.Count > 0)
                        {
                            chars.RemoveAt(chars.Count - 1);
                            Console.Write("\b \b");
                        }
                        continue;
                    }

                    if (!char.IsControl(key.KeyChar))
                    {
                        chars.Add(key.KeyChar);
                        Console.Write('*');
                    }
                }

                var secret = new string(chars.ToArray());
                if (!string.IsNullOrWhiteSpace(secret))
                {
                    return secret;
                }

                Console.WriteLine("This value is required.");
            }
        }

        /// <summary>
        /// Prompts user to select one of multiple values.
        /// </summary>
        public static string PromptChoice(string label, params string[] choices)
        {
            if (choices == null || choices.Length == 0)
            {
                throw new ArgumentException("At least one choice is required.", nameof(choices));
            }

            Console.WriteLine(label);
            for (var i = 0; i < choices.Length; i++)
            {
                Console.WriteLine($"  {i + 1}. {choices[i]}");
            }

            while (true)
            {
                Console.Write("Choose an option: ");
                var input = Console.ReadLine();
                if (int.TryParse(input, out var index) && index >= 1 && index <= choices.Length)
                {
                    return choices[index - 1];
                }

                Console.WriteLine("Invalid selection.");
            }
        }
    }
}
