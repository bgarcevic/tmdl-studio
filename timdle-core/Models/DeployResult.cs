namespace TmdlStudio.Models
{
    public class DeployResult
    {
        public bool IsSuccess { get; private set; }
        public string Message { get; private set; }
        public DeployChange[] Changes { get; private set; }

        private DeployResult(bool isSuccess, string message, DeployChange[] changes)
        {
            IsSuccess = isSuccess;
            Message = message;
            Changes = changes ?? new DeployChange[0];
        }

        public static DeployResult Success(string message, DeployChange[] changes) => 
            new DeployResult(true, message, changes);

        public static DeployResult Error(string message) => 
            new DeployResult(false, message, null);
    }

    public class DeployChange
    {
        public string ObjectType { get; set; }
        public string ObjectName { get; set; }
        public string ChangeType { get; set; }
    }
}
