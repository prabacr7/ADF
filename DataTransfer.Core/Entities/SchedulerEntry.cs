using System;

namespace DataTransfer.Core.Entities
{
    public class SchedulerEntry
    {
        public int Id { get; set; }
        public string? Cron { get; set; }
        public DateTime LastUpdateDateTime { get; set; }
        public DateTime NextUpdateDatetime { get; set; }
        public int ImportId { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsActive { get; set; } = true;
    }
} 