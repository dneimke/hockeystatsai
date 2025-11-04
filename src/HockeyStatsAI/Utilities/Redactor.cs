using System.Text.RegularExpressions;

namespace HockeyStatsAI.Utilities;

public static class Redactor
{
	public static string RedactSecret(string? value)
	{
		if (string.IsNullOrEmpty(value)) return string.Empty;
		return "[REDACTED]";
	}

	public static string RedactConnectionString(string? conn)
	{
		if (string.IsNullOrEmpty(conn)) return string.Empty;
		// Remove credentials and sensitive parts conservatively
		string redacted = Regex.Replace(conn, @"(?i)(Password|Pwd)=[^;]*", "$1=[REDACTED]");
		redacted = Regex.Replace(redacted, @"(?i)(User\s*Id|Uid)=[^;]*", "$1=[REDACTED]");
		return redacted;
	}
}

