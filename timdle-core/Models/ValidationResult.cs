using System;

namespace TmdlStudio.Models
{
    public class ValidationResult
    {
        public bool IsSuccess { get; private set; }
        public bool IsWarning { get; private set; }
        public string Message { get; private set; }

        private ValidationResult(bool isSuccess, bool isWarning, string message)
        {
            IsSuccess = isSuccess;
            IsWarning = isWarning;
            Message = message;
        }

        public static ValidationResult Success(string message) => new ValidationResult(true, false, message);
        public static ValidationResult Warning(string message) => new ValidationResult(true, true, message);
        public static ValidationResult Error(string message) => new ValidationResult(false, false, message);
    }
}
