using System;
using TmdlStudio.Services;

namespace TmdlStudio.Commands
{
    public static class ValidateCommand
    {
        public static void Execute(string path)
        {
            var result = TmdlService.Validate(path);
            string prefix;

            if (result.IsWarning)
            {
                prefix = "WARNING";
            }
            else if (result.IsSuccess)
            {
                prefix = "SUCCESS";
            }
            else
            {
                prefix = "ERROR";
            }

            Console.WriteLine($"{prefix}: {result.Message}");
        }
    }
}