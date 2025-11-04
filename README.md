# HockeyStatsAI

A CLI tool that translates natural language questions into safe, read-only SQL queries for a SQL Server database and executes them with sensible defaults. Built with .NET 9 and powered by Google's Gemini API.

## Overview

HockeyStatsAI is an interactive console application that allows you to query your hockey statistics database using natural language. Simply ask questions in plain English, and the application will:

1. Translate your question into SQL using the Gemini API
2. Validate the SQL for safety (read-only, SELECT-only)
3. Execute the query against your database
4. Display results in your preferred format (table, CSV, or JSON)

## Prerequisites

Before you begin, ensure you have the following installed and configured:

- **.NET 9 SDK** or later
- **SQL Server** (LocalDB is fine) with a database named `hockeystats`
  - For LocalDB, ensure you have an instance named `MSSQLLocalDB`
- **Google Gemini API Key** from [Google AI Studio](https://makersuite.google.com/app/apikey)

## Getting Started

### 1. Clone the Repository

```bash
git clone <repository-url>
cd HockeyStatsAI
```

### 2. Restore NuGet Packages

```bash
dotnet restore
```

### 3. Configure Your Gemini API Key

The application uses `dotnet user-secrets` to securely store your Gemini API Key during development.

From the project directory (`src/HockeyStatsAI`):

```bash
cd src/HockeyStatsAI
dotnet user-secrets init
dotnet user-secrets set GEMINI_API_KEY "<your-gemini-api-key>"
```

Replace `<your-gemini-api-key>` with your actual API key from Google AI Studio.

### 4. Configure Database Connection

You can store the connection string in user-secrets (recommended) or in `appsettings.json`.

**Option 1: Using user-secrets (recommended):**

```bash
dotnet user-secrets set "ConnectionStrings:HockeyStatsDb" "<your-connection-string>"
```

**Option 2: Using appsettings.json:**

Edit `src/HockeyStatsAI/appsettings.json` and update the connection string.

**Example connection strings:**
- LocalDB: `Server=(localdb)\MSSQLLocalDB;Database=hockeystats;Trusted_Connection=True;`
- SQL Server: `Server=localhost;Database=hockeystats;User Id=username;Password=password;`
- For read-scale replicas, add: `ApplicationIntent=ReadOnly`

### 5. Run the Application

From the repository root:

```bash
dotnet run --project src/HockeyStatsAI
```

The application will start and prompt you for natural language questions. Try asking questions like:
- "Show me the top 10 goal scorers"
- "What teams have the most wins this season?"
- "List all players with more than 50 points"

## Features

### Safety First

HockeyStatsAI prioritizes database safety with multiple safeguards:

- **SELECT-only queries**: Non-SELECT statements, multiple statements, temp tables, and `SELECT INTO` are blocked
- **Default row limit**: 100 rows (can be overridden)
- **Execution timeout**: 30 seconds
- **SQL validation**: All queries are validated before execution

### CLI Commands

While the application is running, you can use these commands:

- `dryrun on|off`: Toggle execution mode. When on, only prints the generated SQL without executing
- `limit N`: Override the default row limit for SELECT queries
- `format table|csv|json`: Change the output format for query results
- `explain`: Explain the last generated SQL query using the AI model
- `rerun`: Re-run the last SQL query with current options

## Project Structure

```
HockeyStatsAI/
├── src/
│   ├── HockeyStatsAI/          # Main console application
│   │   ├── Core/                # Core business logic
│   │   ├── Infrastructure/      # AI and database integration
│   │   ├── Models/              # Data models
│   │   └── Program.cs           # Application entry point
│   └── HockeyStatsAI.Tests/    # Unit tests
└── docs/                        # Documentation
```

## Running Tests

To run the unit tests:

```bash
dotnet test
```

Tests cover:
- SQL safety validation rules
- Secret redaction in logging
- Schema registry functionality
- Join path finding

## Configuration

### User Secrets

Prefer user-secrets or environment variables for sensitive configuration:

```bash
cd src/HockeyStatsAI
dotnet user-secrets set GEMINI_API_KEY "<your-key>"
dotnet user-secrets set "ConnectionStrings:HockeyStatsDb" "<your-connection-string>"
```

### AppSettings

Update `src/HockeyStatsAI/appsettings.json` for non-sensitive configuration like logging levels.

## Logging

Console logging is enabled by default. Sensitive values (API keys, connection strings) are automatically redacted from logs to prevent accidental exposure.

## Troubleshooting

**Issue: "Connection string not found"**
- Ensure you've set the connection string in user-secrets or `appsettings.json`
- Verify the database exists and is accessible

**Issue: "Gemini API error"**
- Verify your API key is correct and has sufficient quota
- Check your internet connection

**Issue: "SQL validation failed"**
- The generated SQL may contain unsafe operations
- Try rephrasing your question or use `dryrun on` to see the generated SQL

## Contributing

Contributions are welcome! Please ensure all tests pass before submitting a pull request.

## License

[Add your license information here]
