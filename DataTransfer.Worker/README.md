# DataTransfer Worker Service

A .NET 7 background service that continuously polls a SQL Server database for scheduled data import jobs and executes them.

## Features

- Polls the Scheduler table every 30 seconds (configurable)
- Processes import jobs when their scheduled time is due
- Supports different transfer modes: Truncate and Insert, Delete and Insert, Append
- Executes pre/post SQL scripts during the import process
- Manages foreign key constraints automatically
- Uses SQL bulk copy for efficient data transfer
- Handles both Windows and SQL Server authentication
- Includes retry policies for transient errors
- Detailed logging of all operations

## Configuration

Configure the service in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=DataTransferDB;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "WorkerSettings": {
    "PollingIntervalSeconds": 30,
    "MaxConcurrentJobs": 5,
    "DisableForeignKeyConstraints": true
  }
}
```

## Database Schema

The service requires the following tables:

1. **Scheduler**
   - Id (PK, int)
   - Cron (string, optional) - For future cron expression support
   - LastUpdateDateTime (datetime)
   - NextUpdateDatetime (datetime)
   - ImportId (int) - Foreign key to ImportData
   - CreatedDate (datetime)
   - IsActive (bit)

2. **ImportData**
   - ImportId (PK, int)
   - Name (string)
   - FromDataSourceId (int) - Foreign key to DataSource
   - ToDataSourceId (int) - Foreign key to DataSource
   - FromTable (string)
   - ToTable (string)
   - Query (string, optional) - Custom query instead of table name
   - FromColumnList (string) - Comma-separated list of source columns
   - ToColumnList (string) - Comma-separated list of destination columns
   - MappedColumnList (string, optional) - Comma-separated list of constant values
   - BeforeQuery (string, optional) - SQL to execute before transfer
   - AfterQuery (string, optional) - SQL to execute after transfer
   - IsTruncate (bit) - Whether to truncate destination table
   - IsDelete (bit) - Whether to delete from destination table
   - CreatedDate (datetime)
   - IsActive (bit)

3. **DataSource**
   - DataSourceId (PK, int)
   - DatasourceName (string)
   - ServerName (string)
   - UserName (string)
   - Password (string)
   - AuthenticationType (string) - "Windows Authentication" or "SQL Authentication"
   - DefaultDatabaseName (string)
   - UserId (int, optional) - For user tracking
   - CreatedDate (datetime)
   - IsActive (bit)

## Deployment

1. Build the project:
   ```
   dotnet build
   ```

2. Publish the project:
   ```
   dotnet publish -c Release -o ./publish
   ```

3. Install as a Windows Service using SC:
   ```
   sc create DataTransferService binPath= "C:\path\to\publish\DataTransfer.Worker.exe"
   sc start DataTransferService
   ```

Or use the .NET Worker Service template deployment options for Linux/Docker environments. 