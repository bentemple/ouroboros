using System.Text.Json;

namespace Ouroboros;

/// <summary>
/// Remembers which OIDC subject first logged in as each user_map username.
///
/// Usernames are mutable in most identity providers, so on their own they are a weak claim to a
/// headscale user - renaming somebody to "sink" would hand them sink's devices. A subject claims a
/// username the first time it logs in and keeps it from then on, whatever the provider calls it
/// later, until an admin releases it.
/// </summary>
public static class Bindings
{
	private static readonly string FilePath = Path.Combine(Config.DataPath, "bindings.json");

	private static readonly object Gate = new();

	// subject -> user_map username
	private static readonly Dictionary<string, string> Owners = Load();

	private static Dictionary<string, string> Load()
	{
		if (!File.Exists(FilePath)) return new Dictionary<string, string>();

		return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(FilePath))
			?? new Dictionary<string, string>();
	}

	private static void Save()
	{
		Directory.CreateDirectory(Config.DataPath);

		// write beside the real file and move it into place, so a crash mid-write can't truncate it
		var tmp = FilePath + ".tmp";
		File.WriteAllText(tmp, JsonSerializer.Serialize(Owners, new JsonSerializerOptions { WriteIndented = true }));
		File.Move(tmp, FilePath, true);
	}

	/// <summary>
	/// The username this subject owns, if any. Read only - this is the per-request lookup.
	/// </summary>
	public static string? UsernameFor(string sub)
	{
		lock (Gate) return Owners.GetValueOrDefault(sub);
	}

	/// <summary>
	/// Resolves a fresh login to the username it may use, claiming it for this subject if nobody holds
	/// it yet. Null means this subject may not log in at all.
	/// </summary>
	public static string? Claim(string sub, string? claimedUsername)
	{
		lock (Gate)
		{
			// a subject that already owns a username keeps it, under whatever name it logs in as now
			if (Owners.TryGetValue(sub, out var owned))
				return Config.C.user_map.ContainsKey(owned) ? owned : null;

			if (claimedUsername == null || !Config.C.user_map.ContainsKey(claimedUsername))
				return null;

			// someone else got here first, and only an admin can move it
			if (Owners.ContainsValue(claimedUsername))
				return null;

			Owners[sub] = claimedUsername;
			Save();

			return claimedUsername;
		}
	}

	/// <summary>
	/// Every binding, as subject -> username.
	/// </summary>
	public static Dictionary<string, string> All
	{
		get { lock (Gate) return new Dictionary<string, string>(Owners); }
	}

	/// <summary>
	/// Releases a username. The next subject to log in as it claims it, so this is only half of
	/// re-associating a username - the new person has to log in before the old one does.
	/// </summary>
	public static void Release(string username)
	{
		lock (Gate)
		{
			foreach (var sub in Owners.Where(kv => kv.Value == username).Select(kv => kv.Key).ToArray())
				Owners.Remove(sub);

			Save();
		}
	}
}
