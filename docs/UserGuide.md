# SQL Data Transfer Wizard User Guide

This guide provides instructions on how to set up and use the SQL Data Transfer Wizard.

# SQL Data Transfer Wizard

A web-based tool to transfer and transform data between two SQL Server databases using Angular, ASP.NET Core, and a background worker — with cron-based scheduling and AI-assisted configuration.

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