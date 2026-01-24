using System;
using TmdlStudio.Services;

namespace TmdlStudio.Commands
{
    public static class ValidateCommand
    {
        public static void Execute(string path)
        {
            var result = TmdlService.Validate(path);
            var prefix = result.IsSuccess ? "SUCCESS" : "ERROR";
            Console.WriteLine($"{prefix}: {result.Message}");
        }
    }
}