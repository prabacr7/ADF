# SQL Data Transfer Tool

A web-based application for transferring data between SQL Server databases.

## Tech Stack

### Frontend
- Angular 16+
- Angular Material
- TypeScript
- SCSS

### Backend
- ASP.NET Core 7 Web API
- Dapper (for data access)
- SOLID principles
- Clean Architecture

### Database
- SQL Server (source and destination)

## Features

- Configure source and destination SQL Server connections
- Browse and select tables for transfer
- Map columns between source and destination tables
- Execute custom SQL before and after transfer operations
- Support for three transfer modes:
  - Truncate & Insert
  - Delete & Insert
  - Append
- Monitor transfer progress in real-time
- View logs and execution results
- User-friendly interface with step-by-step wizard

## Project Structure

### Backend

The backend follows a clean architecture approach with these layers:

- **API**: Controllers, DTO mappings, and API configuration
- **Application**: Application services, commands/queries, and handlers
- **Core**: Domain entities, interfaces, and business logic
- **Infrastructure**: Data access, external service implementations

### Frontend

The frontend is organized using a feature-based approach:

- **Core**: Models, interfaces, and core services
- **Features**: Feature modules with their components, services, and routes
- **Shared**: Shared components, directives, and pipes

## Setup and Installation

### Prerequisites

- .NET 7.0 SDK
- Node.js and npm
- SQL Server (or Azure SQL Database)
- Angular CLI

### Backend Setup

1. Clone the repository
2. Navigate to the API directory: `cd DataTransferApp/DataTransfer.API`
3. Restore dependencies: `dotnet restore`
4. Update the connection strings in `appsettings.json`
5. Run the API: `dotnet run`

### Frontend Setup

1. Navigate to the UI directory: `cd DataTransferApp/DataTransfer.UI`
2. Install dependencies: `npm install`
3. Start the Angular app: `ng serve`
4. Open your browser to `http://localhost:4200`

## Deployment

The application can be deployed to Azure with the following components:

- Azure App Service (for frontend and backend)
- Azure SQL Database (for optional logging)
- Azure Key Vault (for storing connection strings)
- Azure Active Directory (for optional authentication)

## License

This project is licensed under the MIT License - see the LICENSE file for details. 