using System;

namespace TmdlStudio.Models
{
    public class ValidationResult
    {
        public bool IsSuccess { get; private set; }
        public string Message { get; private set; }

        private ValidationResult(bool isSuccess, string message)
        {
            IsSuccess = isSuccess;
            Message = message;
        }

        public static ValidationResult Success(string message) => new ValidationResult(true, message);
        public static ValidationResult Error(string message) => new ValidationResult(false, message);
    }
}
