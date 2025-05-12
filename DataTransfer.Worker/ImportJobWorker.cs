using DataTransfer.Core.Entities;
using DataTransfer.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NCrontab;

namespace DataTransfer.Worker
{
    public class ImportJobWorker : BackgroundService
    {
        private readonly ILogger<ImportJobWorker> _logger;
        private readonly ISchedulerRepository _schedulerRepository;
        private readonly IImportDataRepository _importDataRepository;
        private readonly IImportExecutor _importExecutor;
        private readonly WorkerSettings _settings;

        // Store the last execution time of cron jobs to avoid repeated execution
        private Dictionary<int, DateTime> _lastCronExecution = new Dictionary<int, DateTime>();

        public ImportJobWorker(
            ILogger<ImportJobWorker> logger,
            ISchedulerRepository schedulerRepository,
            IImportDataRepository importDataRepository,
            IImportExecutor importExecutor,
            IOptions<WorkerSettings> settings)
        {
            _logger = logger;
            _schedulerRepository = schedulerRepository;
            _importDataRepository = importDataRepository;
            _importExecutor = importExecutor;
            _settings = settings.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ImportJobWorker starting at: {time}", DateTimeOffset.Now);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Process scheduled jobs from the scheduler table
                    await ProcessScheduledJobsAsync(stoppingToken);
                    
                    // Process cron-based jobs directly from ImportData
                    await ProcessCronJobsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing scheduled jobs");
                }

                await Task.Delay(_settings.PollingIntervalSeconds * 1000, stoppingToken);
            }
        }

        private async Task ProcessCronJobsAsync(CancellationToken cancellationToken)
        {
            try
            {
                var currentTime = DateTime.Now;
                _logger.LogDebug("Checking for due cron jobs at {Time}", currentTime);

                // Get all import data with non-null cron expressions
                var cronJobs = await _importDataRepository.GetImportsWithCronJobAsync(cancellationToken);
                int jobCount = 0;

                foreach (var importJob in cronJobs)
                {
                    try
                    {
                        // Skip if CronJob is empty
                        if (string.IsNullOrWhiteSpace(importJob.CronJob))
                        {
                            continue;
                        }

                        // Parse the cron expression
                        CrontabSchedule schedule;
                        try
                        {
                            schedule = CrontabSchedule.Parse(importJob.CronJob);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Invalid cron expression '{CronExpression}' for ImportId {ImportId}", 
                                importJob.CronJob, importJob.ImportId);
                            continue;
                        }

                        // Get last execution time or use a default
                        DateTime lastRun = DateTime.MinValue;
                        if (_lastCronExecution.ContainsKey(importJob.ImportId))
                        {
                            lastRun = _lastCronExecution[importJob.ImportId];
                        }

                        // Calculate next occurrence after last run
                        var nextOccurrence = schedule.GetNextOccurrence(lastRun);

                        // If the next occurrence is in the past relative to current time, execute the job
                        if (nextOccurrence <= currentTime)
                        {
                            jobCount++;
                            _logger.LogInformation("Processing cron job for import {ImportId} with expression '{CronExpression}'", 
                                importJob.ImportId, importJob.CronJob);

                            // Execute the import
                            bool success = await _importExecutor.ExecuteImportAsync(importJob, cancellationToken);

                            // Update the last execution time
                            _lastCronExecution[importJob.ImportId] = currentTime;

                            if (success)
                            {
                                _logger.LogInformation("Successfully executed cron job for import {ImportId}", importJob.ImportId);
                            }
                            else
                            {
                                _logger.LogWarning("Failed to execute cron job for import {ImportId}", importJob.ImportId);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing cron job for import {ImportId}", importJob.ImportId);
                    }
                }

                if (jobCount > 0)
                {
                    _logger.LogInformation("Processed {Count} cron jobs", jobCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ProcessCronJobsAsync");
            }
        }

        private async Task ProcessScheduledJobsAsync(CancellationToken cancellationToken)
        {
            var currentTime = DateTime.UtcNow;
            _logger.LogDebug("Checking for due jobs at {Time}", currentTime);

            var dueJobs = await _schedulerRepository.GetDueJobsAsync(currentTime, cancellationToken);
            int jobCount = 0;

            foreach (var job in dueJobs)
            {
                try
                {
                    jobCount++;
                    _logger.LogInformation("Processing scheduled job {SchedulerId} for import {ImportId}", job.Id, job.ImportId);
                    
                    // Get the complete import data
                    var importData = await _importDataRepository.GetImportDataWithSourcesAsync(job.ImportId, cancellationToken);
                    
                    if (importData == null)
                    {
                        _logger.LogWarning("Import data not found for ID: {ImportId}", job.ImportId);
                        continue;
                    }

                    // Execute the import
                    bool success = await _importExecutor.ExecuteImportAsync(importData, cancellationToken);
                    
                    // Update the last execution time regardless of success to prevent continuous retries of failing jobs
                    await _schedulerRepository.UpdateLastExecutionTimeAsync(job, currentTime, cancellationToken);
                    
                    if (success)
                    {
                        _logger.LogInformation("Successfully executed scheduled job {SchedulerId} for import {ImportId}", 
                            job.Id, job.ImportId);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to execute scheduled job {SchedulerId} for import {ImportId}", 
                            job.Id, job.ImportId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing scheduled job {SchedulerId} for import {ImportId}", 
                        job.Id, job.ImportId);
                }
            }

            if (jobCount > 0)
            {
                _logger.LogInformation("Processed {Count} scheduled jobs", jobCount);
            }
        }
    }
} 