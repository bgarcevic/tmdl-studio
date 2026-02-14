namespace TmdlStudio.Models
{
    public class DeployResult
    {
        public bool IsSuccess { get; private set; }
        public string Message { get; private set; }

        private DeployResult(bool isSuccess, string message)
        {
            IsSuccess = isSuccess;
            Message = message;
        }

        public static DeployResult Success(string message) => 
            new DeployResult(true, message);

        public static DeployResult Error(string message) => 
            new DeployResult(false, message);
    }
}
