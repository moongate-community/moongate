namespace Moongate.Http.Plugin.Data.Api;

/// <summary>Who the caller's token says they are. Deliberately not their characters — see PlayerEndpoints.</summary>
public sealed record PlayerMeResponse(string Username, string Level);
