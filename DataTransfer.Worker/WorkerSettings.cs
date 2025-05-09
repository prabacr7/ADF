namespace DataTransfer.Worker
{
    public class WorkerSettings
    {
        public int PollingIntervalSeconds { get; set; } = 30;
        public int MaxConcurrentJobs { get; set; } = 5;
        public bool DisableForeignKeyConstraints { get; set; } = true;
    }
} 