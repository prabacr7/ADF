using DataTransfer.Core.Interfaces;
using DataTransfer.Infrastructure.Repositories;
using DataTransfer.Infrastructure.Services;
using DataTransfer.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Polly;
using System;

var builder = Host.CreateApplicationBuilder(args);

// Add configuration
builder.Services.Configure<WorkerSettings>(
    builder.Configuration.GetSection("WorkerSettings"));

// Add repositories
builder.Services.AddTransient<ISchedulerRepository, SchedulerRepository>();
builder.Services.AddTransient<IImportDataRepository, ImportDataRepository>();

// Add services
builder.Services.AddTransient<IImportExecutor, DataTransferExecutor>();
builder.Services.AddTransient<ForeignKeyHelper>();
builder.Services.AddTransient<IEncryptionService, EncryptionService>();
builder.Services.AddTransient<IConnectionStringManager, ConnectionStringManager>();

// Add retry policies
builder.Services.AddTransient<IAsyncPolicy>(provider => {
    return Policy
        .Handle<Exception>()
        .WaitAndRetryAsync(
            3, // Retry count
            retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (exception, timeSpan, retryCount, context) => {
                var logger = provider.GetService<ILogger<Program>>();
                logger?.LogWarning(exception, "Retry {RetryCount} after {RetrySeconds}s due to error", 
                    retryCount, timeSpan.TotalSeconds);
            });
});

// Register worker
builder.Services.AddHostedService<ImportJobWorker>();

var host = builder.Build();
host.Run();
