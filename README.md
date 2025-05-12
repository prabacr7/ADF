# AI-Assisted SQL Data Transfer Wizard

## 1. Problem Addressed

Organizations frequently need to synchronize or migrate data between different SQL Server instances or databases. This process often involves repetitive and error-prone tasks:

*   **Manual Scripting:** Writing custom SQL scripts for each transfer is time-consuming, difficult to maintain, and requires significant SQL expertise.
*   **Complex ETL Tools:** Traditional ETL (Extract, Transform, Load) tools can be overly complex, expensive, and have a steep learning curve for simple-to-moderate transfer tasks.
*   **Lack of Scheduling:** Manually running transfers is inefficient for recurring synchronization needs (e.g., updating a reporting database nightly).
*   **Configuration Management:** Managing connection strings, table mappings, and transfer logic securely and consistently across different jobs is challenging.
*   **Error Handling & Logging:** Implementing robust error handling and logging for manual scripts requires extra effort.
*   **Security Risks:** Storing database credentials insecurely in scripts or configuration files poses a significant security risk.

The SQL Data Transfer Wizard aims to simplify and automate these SQL Server data transfer processes through an intuitive web interface, reducing manual effort, minimizing errors, and enhancing security.

## 2. How It Works

The SQL Data Transfer Wizard provides a step-by-step web interface built with Angular, backed by an ASP.NET Core API and a .NET Worker Service.

**Core Components:**

1.  **Angular Frontend:** A user-friendly web application where users configure and manage data transfers.
    *   Guides users through defining source/destination connections.
    *   Allows browsing tables and mapping columns visually.
    *   Provides options for transfer strategies (Truncate & Insert, Delete & Insert, Append).
    *   Enables configuration of custom SQL scripts to run before or after the main transfer.
    *   Includes an interface to define **cron expressions** for scheduling recurring transfers.
2.  **ASP.NET Core API:** The central hub that handles user requests from the frontend.
    *   Manages database connection profiles, securely storing credentials using **AES encryption**.
    *   Validates user input, including **cron expression syntax** using the `Cronos` library.
    *   Saves transfer configurations (mappings, strategies, schedules) to a dedicated SQL Server database (`ImportData` table).
    *   Provides endpoints for testing connections, listing tables/columns, saving configurations, and viewing transfer history.
    *   Calculates the `NextRunDateTime` based on the provided `CronJob` expression.
3.  **.NET Worker Service:** A background service responsible for executing scheduled transfers.
    *   Periodically polls the `ImportData` table in the application database.
    *   Identifies jobs whose `NextRunDateTime` is due.
    *   Retrieves the full configuration for the due job.
    *   Executes the data transfer using efficient methods like `SqlBulkCopy`.
    *   Runs any configured pre/post SQL scripts.
    *   Updates the `LastRunDateTime` and calculates the next `NextRunDateTime` upon completion.
    *   Logs execution status, rows transferred, and any errors to a `TransferLog` table.
4.  **SQL Server Database:** Used for storing the application's own configuration data (connections, import jobs, logs) and as the source/destination for user data transfers.

**Typical Workflow:**

1.  **Connect:** User defines and saves connection details for source and target SQL Server databases. Passwords are encrypted.
2.  **Configure:** User creates a new "Import Job", selecting source/target connections, tables, and mapping columns. They choose a transfer strategy and can add custom SQL.
3.  **Schedule (Optional):** User enters a cron expression (e.g., `0 2 * * *` for 2 AM daily) to automate the transfer. The API validates the syntax and calculates the first run time.
4.  **Save:** The configuration, including the cron schedule, is saved via the API to the database.
5.  **Execute:**
    *   **Manual:** User triggers the job directly via the UI/API.
    *   **Scheduled:** The Worker Service detects the job is due based on `NextRunDateTime`, retrieves the configuration, and executes the transfer.
6.  **Monitor:** User can view the status and history of transfers through the UI, which queries the `TransferLog` table.

## 3. How AI Tools Were Used in Development

AI tools, primarily **ChatGPT** and **Cursor IDE**, played a significant role in accelerating development, improving code quality, and implementing complex features:

1.  **Cron Scheduling Implementation:**
    *   **Challenge:** Selecting the right .NET library for cron parsing, implementing robust parsing logic, and calculating next run times accurately considering UTC.
    *   **AI Contribution:** Provided guidance on using the `Cronos` library in the API for validation/initial calculation and `NCrontab.Signed` in the Worker for robust parsing. Generated code snippets for parsing expressions, handling potential errors, and calculating `NextRunDateTime` based on `DateTime.UtcNow`. Helped draft unit tests (`XUnit`) for validating various cron patterns.
2.  **Secure Credential Storage:**
    *   **Challenge:** Implementing secure storage for SQL Server connection strings and passwords.
    *   **AI Contribution:** Recommended using AES encryption. Provided C# code examples for encryption/decryption methods, including handling initialization vectors (IV) and keys securely. Guided the integration of this encryption logic into the connection saving/retrieval process in the API and Worker service.
3.  **API Development & Refinement:**
    *   **AI Contribution:** Assisted in designing DTOs (Data Transfer Objects) for API requests/responses. Helped implement validation logic (e.g., for required fields, cron syntax). Generated boilerplate code for controllers and services based on requirements. Suggested improvements for error handling and logging within API endpoints.
4.  **Dapper & SQL Optimization:**
    *   **AI Contribution:** Provided examples of using Dapper for efficient data access. Helped optimize SQL queries used by the API and Worker service, including constructing parameterized queries to prevent SQL injection vulnerabilities, particularly when handling custom user-provided SQL scripts.
5.  **Frontend (Angular) Guidance:**
    *   **AI Contribution:** Offered suggestions on structuring Angular components and services following best practices. Provided examples for using Angular Material components and reactive forms (`FormGroup`, `FormControl`) for the multi-step wizard interface.
6.  **Troubleshooting & Debugging:**
    *   **AI Contribution:** Helped diagnose runtime errors by analyzing stack traces and error messages. Suggested potential causes and fixes for issues encountered during development (e.g., SQL connection problems, parameter mismatches, column mapping errors).
7.  **Documentation Generation:**
    *   **AI Contribution:** Assisted in drafting sections of this `README.md` and other documentation files (`Project_Overview.md`, `CronExamples.md`) based on high-level prompts and conversation history.

By leveraging AI, the development process was streamlined, allowing for faster implementation of features like cron scheduling and encryption, while also promoting better code quality and security practices.

## ✨ Features
- 🔄 Transfer tables between two SQL Server instances
- 📊 Map columns and preview source records
- ⏱️ Schedule recurring jobs using cron expressions
- 🔒 AES encryption for sensitive credentials
- 💬 AI used to generate secure, clean code patterns
- 📝 Custom SQL queries for data transformation
- 🔄 Multiple transfer strategies (truncate, delete-insert, append)
- 📋 Pre/post execution SQL scripts

## 🛠️ Tech Stack
- **Frontend**: Angular 16+, Angular Material, TypeScript, SCSS
- **Backend API**: ASP.NET Core 7, Dapper, Cronos
- **Database**: SQL Server (for both source/destination and app config)
- **Worker Service**: .NET Core Worker (background service)
- **Testing**: XUnit, NCrontab.Signed for cron validation

## 📋 Prerequisites
- .NET 7.0 SDK
- Node.js 16+ and npm
- SQL Server 2019+ (or Azure SQL)
- Angular CLI 16+
- Visual Studio 2022 or VS Code

## 🚀 Getting Started

### Database Setup
1. Run the SQL script to create the application database:
```sql
-- In SQL Server Management Studio:
CREATE DATABASE SQLDataTransfer;
GO
```
2. Run the database schema script from `sql/DatabaseSchema.sql`

### Backend API Setup
1. Navigate to the API directory
```bash
cd src/DataTransfer.API
```
2. Update connection strings in `appsettings.json` with your SQL Server details
3. Restore and run the API
```bash
dotnet restore
dotnet run
```
4. The API will be available at `https://localhost:7157` and `http://localhost:5157`

### Frontend Setup
1. Navigate to the UI directory
```bash
cd src/DataTransfer.UI
```
2. Install dependencies
```bash
npm install
```
3. Start the Angular app
```bash
ng serve
```
4. Access the application at `http://localhost:4200`

## 🔄 Running the Worker Service
The Worker Service handles scheduled data transfers based on cron expressions:

1. Navigate to the Worker directory
```bash
cd src/DataTransfer.Worker
```
2. Update connection strings in `appsettings.json`
3. Run the worker service
```bash
dotnet run
```

## 🌐 API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/connection/test` | POST | Test SQL server connection |
| `/api/connection/save` | POST | Save connection details with encryption |
| `/api/connection/list` | GET | Get all saved connections |
| `/api/table/list` | GET | Get tables from a database |
| `/api/column/map` | POST | Map columns between tables |
| `/api/import/save` | POST | Save import configuration with cron schedule |
| `/api/import/execute/{id}` | POST | Execute import immediately |
| `/api/import/history/{id}` | GET | Get execution history |

## ⏱️ Cron Job Scheduling Support
The application supports standard cron expressions for scheduling recurring data transfers:

- Format: `* * * * *` (minute hour day month weekday)
- Examples:
  - `*/10 * * * *` - Every 10 minutes
  - `0 */2 * * *` - Every 2 hours
  - `0 9-17 * * 1-5` - Hourly from 9AM-5PM, Monday to Friday
  - `0 0 * * 0` - Every Sunday at midnight
  - `0 0 1 * *` - First day of each month at midnight

The Worker Service evaluates expressions and processes jobs when their time arrives.

## 📁 Folder Structure
```
DataTransferApp/
├── DataTransfer.API/          # ASP.NET Core API
│   ├── Controllers/           # API endpoints
│   ├── Models/                # DTOs and request models
│   ├── Migrations/            # Database migration scripts
│   └── Services/              # Business logic
├── DataTransfer.Worker/       # Background Worker Service
│   ├── Models/                # Worker-specific models
│   ├── Services/              # Transfer execution services
│   └── Worker.cs              # Main worker background service
├── DataTransfer.Core/         # Shared domain models and interfaces
├── DataTransfer.Infrastructure/# Data access and external services
├── DataTransfer.UI/           # Angular frontend
│   ├── src/app/components/    # UI components
│   ├── src/app/services/      # API client services
│   └── src/app/models/        # TypeScript models
└── sql/                       # SQL scripts for setup
```

## 📄 License
MIT License

## 👤 Authors
- Development Team
- AI-assisted with ChatGPT & Cursor IDE 