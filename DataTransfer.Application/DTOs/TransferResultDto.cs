using DataTransfer.Core.Entities;

namespace DataTransfer.Application.DTOs
{
    public class TransferResultDto
    {
        public bool IsSuccess { get; set; }
        public long RowsTransferred { get; set; }
        public TimeSpan Duration { get; set; }
        public List<string> Messages { get; set; } = new List<string>();
        public string? ErrorMessage { get; set; }

        public static TransferResultDto FromEntity(TransferResult entity)
        {
            return new TransferResultDto
            {
                IsSuccess = entity.IsSuccess,
                RowsTransferred = entity.RowsTransferred,
                Duration = entity.Duration,
                Messages = entity.Messages.ToList(),
                ErrorMessage = entity.Error?.Message
            };
        }
    }
} 