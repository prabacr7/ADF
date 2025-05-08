namespace DataTransfer.Core.Entities
{
    public class TransferResult
    {
        public bool IsSuccess { get; set; }
        public long RowsTransferred { get; set; }
        public TimeSpan Duration { get; set; }
        public List<string> Messages { get; set; } = new List<string>();
        public Exception? Error { get; set; }
    }
} 