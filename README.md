# HockeyStatsAI

A CLI that translates natural language questions into safe, read-only SQL for a SQL Server database and executes them with sensible defaults.

## Prerequisites
- .NET 9 SDK
- SQL Server (LocalDB is fine) with the `hockeystats` DB
- Google Gemini API key

## Setup
1. Restore packages:
```bash
dotnet restore
```
2. Set user-secrets in the project directory (`src/HockeyStatsAI`):
```bash
# from src/HockeyStatsAI
dotnet user-secrets set GEMINI_API_KEY "<your-gemini-api-key>"
# If you prefer storing connection string in user-secrets instead of appsettings.json
dotnet user-secrets set "ConnectionStrings:HockeyStatsDb" "<your-connection-string>"
```
3. Build and run:
```bash
dotnet run --project src/HockeyStatsAI
```

## Safety
- SELECT-only. Non-SELECT statements, multiple statements, temp tables, and `SELECT INTO` are blocked.
- Default row limit: 100 (can be overridden with the `limit` command).
- Execution timeout: 30s.

## CLI Commands
- `dryrun on|off`: Toggle execution. When on, only prints SQL.
- `limit N`: Override default row limit for SELECTs.
- `format table|csv|json`: Change output format.
- `explain`: Explain the last SQL using the model.
- `rerun`: Re-run the last SQL with current options.

## Logging
- Console logging is enabled. Sensitive values are not printed.

## Tests
Run unit tests:
```bash
dotnet test
```
Tests cover SQL safety rules and secret redaction.

## Configuration
- Update `appsettings.json` as needed. Prefer user-secrets or environment variables for secrets.
- Suggested connection string option: `ApplicationIntent=ReadOnly` for read-scale replicas.

# HockeyStats Natural Language Query Tool

This project demonstrates a .NET console application that translates natural language questions into SQL queries using the Gemini API and executes them against a local SQL Server database.

## Getting Started

Follow these steps to get the application up and running on your local machine.

### Prerequisites

* .NET SDK (version 9.0 or later)
* SQL Server LocalDB instance named `MSSQLLocalDB` with a database named `hockeystats`.
* A Gemini API Key from Google AI Studio.

### Project Structure

The project is organized into the following directories:

- `src/HockeyStatsAI`: The main console application.
- `src/HockeyStatsAI.Tests`: The unit tests for the application.

### Setup

1. **Clone the repository:**

    ```bash
    git clone <repository-url>
    cd <repository-name>
    ```

2. **Install NuGet packages:**

    ```bash
    dotnet restore src/HockeyStatsAI.sln
    ```

3. **Configure your Gemini API Key:**
    The application uses `dotnet user-secrets` to securely store your Gemini API Key during development. Replace `YOUR_GEMINI_API_KEY` with your actual key.

    ```bash
    dotnet user-secrets init --project src/HockeyStatsAI
    dotnet user-secrets set "GEMINI-API-KEY" "YOUR_GEMINI_API_KEY" --project src/HockeyStatsAI
    ```

4. **Configure the Database Connection String:**
    The database connection string is stored in `dotnet user-secrets`. Replace `YOUR_CONNECTION_STRING` with your actual SQL Server connection string.

    ```bash
    dotnet user-secrets set "ConnectionStrings:HockeyStatsDb" "YOUR_CONNECTION_STRING" --project src/HockeyStatsAI
    ```

### Running the Application

To run the application, execute the following command from the root directory:

```bash
dotnet run --project src/HockeyStatsAI
```

The application will then:

1. Prompt you for a natural language question.
2. Send the question to the Gemini API.
3. Receive and parse the generated SQL query.
4. Execute the SQL query against your local database.
5. Display the results to the console.

### Running the Tests

To run the unit tests, execute the following command from the root directory:

```bash
dotnet test src/HockeyStatsAI.sln
```
