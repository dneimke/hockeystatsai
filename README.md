# HockeyStats Natural Language Query Tool

This project demonstrates a .NET console application that translates natural language questions into SQL queries using the Gemini API and executes them against a local SQL Server database.

## Getting Started

Follow these steps to get the application up and running on your local machine.

### Prerequisites

* .NET SDK (version 8.0 or later)
* SQL Server LocalDB instance named `MSSQLLocalDB` with a database named `hockeystats`.
* A Gemini API Key from Google AI Studio.

### Setup

1. **Clone the repository:**

    ```bash
    git clone <repository-url>
    cd <repository-name>
    ```

2. **Install NuGet packages:**

    ```bash
    dotnet restore
    ```

3. **Configure your Gemini API Key:**
    The application uses `dotnet user-secrets` to securely store your Gemini API Key during development. Replace `YOUR_GEMINI_API_KEY` with your actual key.

    ```bash
    dotnet user-secrets init
    dotnet user-secrets set "GEMINI-API-KEY" "YOUR_GEMINI_API_KEY"
    ```

4. **Configure the Database Connection String:**
    The database connection string is stored in `dotnet user-secrets`. Replace `YOUR_CONNECTION_STRING` with your actual SQL Server connection string.

    ```bash
    dotnet user-secrets set "ConnectionStrings:HockeyStatsDb" "YOUR_CONNECTION_STRING"
    ```

### Running the Application

To run the application, execute the following command in the project root:

```bash
dotnet run
```

The application will then:

1. Send a predefined natural language question to the Gemini API.
2. Receive and parse the generated SQL query.
3. Execute the SQL query against your local database.
4. Display the results to the console.
