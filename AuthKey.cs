namespace Ouroboros;

/// <summary>
/// The key headscale puts in the registration link it prints. 0.29 writes "hskey-authreq-" followed by
/// 24 url-safe random characters; 0.26 and earlier wrote the 24 on their own. Both are accepted.
/// </summary>
public static class AuthKey
{
	private const string Prefix       = "hskey-authreq-";
	private const int    RandomLength = 24;

	/// <summary>
	/// Pulls the key out of whatever was pasted - the key alone, or the whole URL around it. Null when
	/// it does not look like a key, so it can never reach the headscale CLI as an argument.
	/// </summary>
	public static string? Parse(string? input)
	{
		var key = input?.Trim().TrimEnd('/');
		if (string.IsNullOrEmpty(key)) return null;

		// so that the printed link can be pasted whole, not just the key at the end of it
		var lastSlash = key.LastIndexOf('/');
		if (lastSlash >= 0) key = key[(lastSlash + 1)..];

		var random = key.StartsWith(Prefix, StringComparison.Ordinal) ? key[Prefix.Length..] : key;

		return random.Length == RandomLength && random.All(IsUrlSafe) ? key : null;
	}

	// headscale generates these with GenerateRandomStringURLSafe, so base64url's alphabet
	private static bool IsUrlSafe(char c) => char.IsAsciiLetterOrDigit(c) || c is '-' or '_';
}
