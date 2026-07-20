namespace Moongate.Http.Plugin.Data.Console;

/// <summary>One server-sent event on a console stream: an <c>Event</c> name (ready/line/done) and its text.</summary>
public sealed record ConsoleStreamEvent(string Event, string Text);
