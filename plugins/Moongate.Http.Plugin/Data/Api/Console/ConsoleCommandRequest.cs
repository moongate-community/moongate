namespace Moongate.Http.Plugin.Data.Api.Console;

/// <summary>A console command to run and the SSE connection its output should stream to.</summary>
public sealed record ConsoleCommandRequest(string Command, string ConnectionId);
