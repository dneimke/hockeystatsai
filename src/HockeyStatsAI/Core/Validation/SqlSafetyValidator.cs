using System.Text.RegularExpressions;

namespace HockeyStatsAI.Core.Validation;

/// <summary>
/// Validates SQL queries to ensure they are safe SELECT-only statements.
/// Prevents execution of dangerous SQL commands like DROP, DELETE, INSERT, etc.
/// </summary>
/// <remarks>
/// This static class is used in <see cref="Program"/> to validate SQL queries generated
/// by <see cref="GeminiTranslator"/> before execution. It implements a deny-list approach,
/// checking for dangerous keywords and constructs that could modify or damage the database.
/// The validator only allows SELECT statements and blocks:
/// - Data modification commands (INSERT, UPDATE, DELETE, MERGE)
/// - Schema modification commands (ALTER, DROP, TRUNCATE, CREATE)
/// - Execution commands (EXEC, EXECUTE)
/// - Security commands (GRANT, REVOKE, DENY)
/// - Database commands (USE, RESTORE, BACKUP)
/// - SELECT INTO statements
/// - Temporary table references
/// - Multiple statements (semicolon-separated)
/// </remarks>
public static class SqlSafetyValidator
{
	// Deny-list of dangerous commands/constructs
	private static readonly Regex s_forbiddenKeywords = new(
		@"\b(INSERT|UPDATE|DELETE|MERGE|ALTER|DROP|TRUNCATE|CREATE|EXEC|EXECUTE|GRANT|REVOKE|DENY|USE|RESTORE|BACKUP)\b",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

	// SELECT INTO and temp tables
	private static readonly Regex s_selectInto = new(
		@"\bSELECT\b[\s\S]*?\bINTO\b",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

	private static readonly Regex s_tempTable = new(
		@"[#]{1,2}[A-Za-z_]",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

	// Multiple statements via semicolon (allow optional single trailing semicolon after whitespace)
	private static readonly Regex s_midstreamSemicolon = new(
		@";\s*\S",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

	// Basic check that the top-level statement starts with SELECT (after removing comments/whitespace)
	private static readonly Regex s_leadingSelect = new(
		@"^\s*SELECT\b",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

	/// <summary>
	/// Validates that a SQL query is a safe SELECT-only statement.
	/// </summary>
	/// <param name="sql">The SQL query to validate.</param>
	/// <param name="violationReason">If validation fails, contains the reason why the SQL is unsafe.</param>
	/// <returns>
	/// True if the SQL is safe (SELECT-only, no dangerous keywords, no multiple statements);
	/// false otherwise with the reason in <paramref name="violationReason"/>.
	/// </returns>
	/// <remarks>
	/// This method performs multiple checks:
	/// 1. Ensures the query is not empty
	/// 2. Verifies the query starts with SELECT (after removing comments)
	/// 3. Blocks SELECT INTO statements
	/// 4. Blocks temporary table references (#temp, ##global)
	/// 5. Blocks multiple statements (semicolon-separated)
	/// 6. Blocks dangerous keywords (INSERT, UPDATE, DELETE, DROP, etc.)
	/// Used in <see cref="Program"/> to validate queries before execution by <see cref="SqlExecutor"/>.
	/// </remarks>
	public static bool IsSafeSelect(string? sql, out string violationReason)
	{
		violationReason = string.Empty;
		if (string.IsNullOrWhiteSpace(sql))
		{
			violationReason = "Empty SQL.";
			return false;
		}

		string normalized = StripComments(sql).Trim();

		if (!s_leadingSelect.IsMatch(normalized))
		{
			violationReason = "Only SELECT statements are allowed.";
			return false;
		}

		// Forbid SELECT INTO and temp tables
		if (s_selectInto.IsMatch(normalized))
		{
			violationReason = "SELECT INTO is not allowed.";
			return false;
		}

		if (s_tempTable.IsMatch(normalized))
		{
			violationReason = "Temporary tables are not allowed.";
			return false;
		}

		// Forbid midstream semicolons (indicates multiple statements). Trailing semicolon alone is ok.
		if (s_midstreamSemicolon.IsMatch(RemoveTrailingSemicolon(normalized)))
		{
			violationReason = "Multiple statements are not allowed.";
			return false;
		}

		// Forbid obviously dangerous keywords even if inside a larger SELECT
		if (s_forbiddenKeywords.IsMatch(normalized))
		{
			violationReason = "Dangerous SQL keywords detected.";
			return false;
		}

		return true;
	}

	/// <summary>
	/// Removes SQL comments from the input string to normalize it for validation.
	/// </summary>
	/// <param name="input">The SQL string with comments.</param>
	/// <returns>The SQL string with comments removed (replaced with spaces).</returns>
	/// <remarks>
	/// Removes both block comments (/* ... */) and line comments (-- ...).
	/// This is necessary to ensure validation checks work correctly even when
	/// comments are used to bypass security checks.
	/// </remarks>
	private static string StripComments(string input)
	{
		// Remove /* */ block comments and -- line comments conservatively
		string noBlock = Regex.Replace(input, @"/\*[\s\S]*?\*/", " ", RegexOptions.Compiled);
		string noLine = Regex.Replace(noBlock, @"--.*?$", " ", RegexOptions.Multiline | RegexOptions.Compiled);
		return noLine;
	}

	/// <summary>
	/// Removes trailing semicolons from the input string.
	/// </summary>
	/// <param name="input">The SQL string potentially ending with a semicolon.</param>
	/// <returns>The SQL string with trailing semicolons removed.</returns>
	/// <remarks>
	/// This allows a single trailing semicolon (which is valid SQL syntax) while
	/// still detecting multiple statements separated by semicolons.
	/// </remarks>
	private static string RemoveTrailingSemicolon(string input)
	{
		return Regex.Replace(input, @";\s*$", string.Empty, RegexOptions.Multiline | RegexOptions.Compiled);
	}
}

