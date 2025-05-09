using DataTransfer.Core.Entities;
using DataTransfer.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DataTransfer.Worker
{
    public class ImportJobWorker : BackgroundService
    {
        private readonly ILogger<ImportJobWorker> _logger;
        private readonly ISchedulerRepository _schedulerRepository;
        private readonly IImportDataRepository _importDataRepository;
        private readonly IImportExecutor _importExecutor;
        private readonly WorkerSettings _settings;

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
                    await ProcessScheduledJobsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing scheduled jobs");
                }

                await Task.Delay(_settings.PollingIntervalSeconds * 1000, stoppingToken);
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