# SQL Data Transfer Wizard — Project Overview

## 🌟 Project Vision

The SQL Data Transfer Wizard represents a modern solution to a persistent problem faced by data engineers and DBAs: efficiently moving and transforming data between SQL Server databases while maintaining control, flexibility, and automation capabilities.

### 🎯 Problem Statement

Organizations often struggle with these data transfer challenges:

- Manual data synchronization between environments is error-prone and time-consuming
- Existing ETL tools are complex, expensive, or require specialized knowledge
- Custom scripts lack flexibility and are difficult to maintain
- Setting up automated data pipelines requires extensive DevOps knowledge
- No visibility into transfer history or execution statistics
- Sensitive connection credentials are often stored insecurely

### 💡 Our Solution

The SQL Data Transfer Wizard provides a comprehensive yet intuitive interface for configuring, executing, and scheduling data transfers between SQL Server databases. The application combines the accessibility of a web interface with the robustness of .NET Core and the flexibility of cron-based scheduling.

## 🔧 Technical Architecture

### System Components

![System Architecture](https://via.placeholder.com/800x400?text=SQL+Data+Transfer+Wizard+Architecture)

The system consists of three main components:

1. **Angular Frontend** - Provides an intuitive UI for configuring connections, mapping tables, and scheduling transfers
2. **ASP.NET Core API** - Handles connection management, data preview, and transfer configuration
3. **Worker Service** - Executes scheduled transfers based on cron expressions

### Data Flow

1. User configures source and target database connections (with encrypted credentials)
2. User selects source/target tables and maps columns
3. User configures transfer options (truncate, custom SQL, etc.)
4. Configuration is saved to the application database with optional cron schedule
5. Worker service polls for due jobs and executes transfers
6. Results and logs are stored for review

## ⚙️ Key Features In Depth

### Connection Management
- Support for SQL Server authentication and Windows authentication
- AES-256 encryption for stored credentials
- Connection testing with detailed error reporting
- Named connection profiles for reuse

### Table Selection and Column Mapping
- Browse available tables in source database
- Preview sample data for mapping verification
- Automatic type compatibility validation
- Smart column name matching suggestions
- Support for custom SQL queries as source

### Transfer Execution Options
- Pre-transfer script execution (e.g., disabling constraints)
- Three transfer modes:
  - **Truncate & Insert** - Empties target table before transferring
  - **Delete & Insert** - Removes matching records before inserting
  - **Append** - Adds source records to target without modification
- Post-transfer script execution (e.g., rebuilding indexes)
- Configurable batch size for optimal performance

### Cron-Based Scheduling
- Standard cron expression format (`* * * * *`)
- Next execution time preview and validation
- Configurable scheduling patterns (minutes, hourly, daily, weekly, monthly)
- Transfer history with execution metrics

### Performance Optimizations
- SqlBulkCopy for efficient data insertion
- Transaction management for data consistency
- Parallel processing capabilities
- Configurable timeouts and retry logic

## 🧠 AI-Assisted Development

This project leveraged AI assistance through ChatGPT and Cursor IDE to enhance development in several areas:

### Cron Expression Implementation
- **Challenge**: Implementing robust cron expression parsing and next-run calculation
- **AI Contribution**: Guided implementation using Cronos library, with validation and error handling
- **Result**: Reliable scheduler with support for complex recurrence patterns

### Security Enhancements
- **Challenge**: Securely storing database credentials
- **AI Contribution**: Recommended AES-256 encryption approach with key management
- **Result**: Robust encryption with proper key handling and salt generation

### SQL Query Generation
- **Challenge**: Dynamically building complex SQL for different scenarios
- **AI Contribution**: Assisted with parameterized query construction to prevent SQL injection
- **Result**: Secure, performant SQL with proper handling of edge cases

### Clean Architecture
- **Challenge**: Maintaining separation of concerns across layers
- **AI Contribution**: Recommended patterns and practices for clean layering
- **Result**: Maintainable codebase with clear responsibilities

## 📊 Database Schema

The application uses the following key tables:

**Connection Table**
```sql
CREATE TABLE [dbo].[Connection](
    [ConnectionId] [int] IDENTITY(1,1) NOT NULL,
    [UserId] [int] NOT NULL,
    [ConnectionName] [nvarchar](100) NOT NULL,
    [ServerName] [nvarchar](100) NOT NULL,
    [DatabaseName] [nvarchar](100) NOT NULL,
    [Authentication] [nvarchar](50) NOT NULL,
    [Username] [nvarchar](100) NULL,
    [Password] [nvarchar](255) NULL,
    [ConnectionString] [nvarchar](max) NULL,
    [EncryptedPassword] [nvarchar](max) NULL,
    [IsActive] [bit] NOT NULL DEFAULT(1),
    [CreatedDate] [datetime] NOT NULL DEFAULT(GETDATE()),
    [ModifiedDate] [datetime] NULL,
    CONSTRAINT [PK_Connection] PRIMARY KEY CLUSTERED ([ConnectionId] ASC)
);
```

**ImportData Table**
```sql
CREATE TABLE [dbo].[ImportData](
    [ImportId] [int] IDENTITY(1,1) NOT NULL,
    [UserId] [int] NOT NULL,
    [FromConnectionId] [int] NOT NULL,
    [ToConnectionId] [int] NOT NULL,
    [FromDataBase] [nvarchar](100) NOT NULL,
    [ToDataBase] [nvarchar](100) NOT NULL,
    [FromTableName] [nvarchar](100) NOT NULL,
    [ToTableName] [nvarchar](100) NOT NULL,
    [Query] [nvarchar](max) NULL,
    [SourceColumnList] [nvarchar](max) NULL,
    [DescColumnList] [nvarchar](max) NULL,
    [ManText] [nvarchar](max) NULL,
    [Description] [nvarchar](500) NULL,
    [IsTruncate] [bit] NOT NULL DEFAULT(0),
    [IsDeleteAndInsert] [bit] NOT NULL DEFAULT(0),
    [BeforeQuery] [nvarchar](max) NULL,
    [AfterQuery] [nvarchar](max) NULL,
    [CreatedDate] [datetime] NOT NULL DEFAULT(GETDATE()),
    [CronJob] [nvarchar](50) NULL,
    [NextRunDateTime] [datetime] NULL,
    [LastRunDateTime] [datetime] NULL,
    CONSTRAINT [PK_ImportData] PRIMARY KEY CLUSTERED ([ImportId] ASC)
);
```

**TransferLog Table**
```sql
CREATE TABLE [dbo].[TransferLog](
    [LogId] [int] IDENTITY(1,1) NOT NULL,
    [ImportId] [int] NOT NULL,
    [StartTime] [datetime] NOT NULL,
    [EndTime] [datetime] NULL,
    [Status] [nvarchar](50) NOT NULL,
    [RowsTransferred] [int] NULL,
    [ErrorMessage] [nvarchar](max) NULL,
    [ExecutedBy] [int] NOT NULL,
    CONSTRAINT [PK_TransferLog] PRIMARY KEY CLUSTERED ([LogId] ASC)
);
```

## 🚀 Development Process

The project followed an Agile development approach:

1. **Research & Design Phase**
   - Requirement gathering and user stories
   - Architecture decisions and technology selection
   - Database schema design

2. **Implementation Phase**
   - Core functionality development
   - UI/UX implementation
   - Integration of components

3. **Cron Job Enhancement**
   - Research on cron expression libraries
   - Implementation of scheduling UI
   - Worker service enhancements
   - Testing with various scheduling patterns

4. **Testing & Refinement**
   - Unit testing for critical components
   - Integration testing of the complete flow
   - Performance optimization
   - Security hardening

## 🔮 Future Enhancements

Planned future enhancements include:

1. **Advanced Transformation Capabilities**
   - Column-level transformations using expressions
   - Support for calculated columns
   - Data type conversions

2. **Enhanced Monitoring**
   - Real-time transfer progress reporting
   - Email notifications for job success/failure
   - Transfer statistics dashboard

3. **Additional Data Sources**
   - Support for MySQL, PostgreSQL, and Oracle
   - Azure Blob Storage integration
   - CSV/Excel file import

4. **Workflow Enhancements**
   - Dependency chains between transfers
   - Conditional execution based on results
   - Retry policies and failure handling

## 📝 Conclusion

The SQL Data Transfer Wizard represents a modern, flexible solution for database data movement with a focus on usability, security, and automation. The application's modular architecture allows for continued enhancement and extension as requirements evolve. The integration of cron-based scheduling provides powerful automation capabilities while maintaining an intuitive user experience.

---

## 🔗 Additional Resources

- [User Guide](./UserGuide.md)
- [API Documentation](./APIReference.md)
- [Cron Expression Examples](./CronExamples.md)
- [Deployment Guide](./DeploymentGuide.md) 