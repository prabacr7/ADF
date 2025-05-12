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

            // Try to ensure database columns exist
            await _schedulerRepository.EnsureNextRunDateTimeColumnExistsAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Process scheduled jobs from the scheduler table (legacy)
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
                var currentTime = DateTime.UtcNow;
                _logger.LogDebug("Checking for due cron jobs at {Time}", currentTime);

                // Get all import data with non-null cron expressions from the repository
                var cronJobs = await _schedulerRepository.GetImportsWithCronJobAsync(cancellationToken);
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

                        // Get last execution time from database or use created date as fallback
                        DateTime lastRun = importJob.LastRunDateTime ?? importJob.CreatedDate;
                        
                        // Calculate next occurrence after last run
                        var nextOccurrence = schedule.GetNextOccurrence(lastRun);

                        // If the next occurrence is in the past relative to current time, execute the job
                        if (nextOccurrence <= currentTime)
                        {
                            // Get the complete import data with sources
                            var completeImportData = await _importDataRepository.GetImportDataWithSourcesAsync(importJob.ImportId, cancellationToken);
                            
                            if (completeImportData == null)
                            {
                                _logger.LogWarning("Import data not found for cron job with ID: {ImportId}", importJob.ImportId);
                                continue;
                            }

                            jobCount++;
                            _logger.LogInformation("Processing cron job for import {ImportId} with expression '{CronExpression}'", 
                                completeImportData.ImportId, importJob.CronJob);

                            // Execute the import
                            bool success = await _importExecutor.ExecuteImportAsync(completeImportData, cancellationToken);

                            // Update last run time in database
                            await _schedulerRepository.UpdateImportLastRunTimeAsync(importJob.ImportId, currentTime, cancellationToken);
                            
                            // Calculate and store next run time
                            var nextRun = schedule.GetNextOccurrence(currentTime);
                            await _schedulerRepository.UpdateNextRunTimeAsync(importJob.ImportId, nextRun, cancellationToken);
                            
                            // Also update in-memory cache
                            _lastCronExecution[importJob.ImportId] = currentTime;

                            if (success)
                            {
                                _logger.LogInformation("Successfully executed cron job for import {ImportId}. Next run at: {NextRun}", 
                                    importJob.ImportId, nextRun);
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
            _logger.LogDebug("Checking for legacy scheduled jobs at {Time}", currentTime);

            var dueJobs = await _schedulerRepository.GetDueJobsAsync(currentTime, cancellationToken);
            int jobCount = 0;

            foreach (var job in dueJobs)
            {
                try
                {
                    jobCount++;
                    _logger.LogInformation("Processing legacy scheduled job {SchedulerId} for import {ImportId}", job.Id, job.ImportId);
                    
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
                    
                    // Also update in the ImportData table if it has a CronJob
                    if (!string.IsNullOrEmpty(importData.CronJob))
                    {
                        await _schedulerRepository.UpdateImportLastRunTimeAsync(importData.ImportId, currentTime, cancellationToken);
                    }
                    
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
                _logger.LogInformation("Processed {Count} legacy scheduled jobs", jobCount);
            }
        }
    }
} 