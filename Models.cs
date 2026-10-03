// ReSharper disable once CheckNamespace

namespace Ouroboros.Models;

public record AuthIndexModel(string? MKey, string ReturnToPretty);

public record AuthNotRegisteredModel(string Login);

public record RegisterIndexModel(AuthedUser User, string TrimmedNK);

/// <param name="InUserMap">false for a binding whose username is no longer configured</param>
public record AdminUserRow(string Username, string HeadscaleName, string? Subject, bool InUserMap);

public record AdminModel(
	string                               DisplayName,
	IEnumerable<AdminUserRow>            Users,
	IEnumerable<Headscale.HeadscaleNode> Nodes);

public record DashboardModel(
	AuthedUser                                                            User,
	IEnumerable<Headscale.HeadscaleNode>                                  YourNodes,
	IEnumerable<Headscale.HeadscaleNode>                                  OtherNodes);