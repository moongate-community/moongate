using Moongate.Server.Abstractions.Data.Notifications;
using Moongate.Server.Abstractions.Interfaces.Notifications;
using Moongate.Server.Abstractions.Types;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;

namespace Moongate.Server.Services.Notifications;

/// <summary>
/// Scriban-backed template registry. Templates are parsed once at registration — the only place Scriban
/// is allowed to fail — and rendering afterwards is pure.
/// </summary>
public sealed class NotificationTemplateService : INotificationTemplateService
{
    private const string SubjectVariable = "subject";
    private const string ContentTypeVariable = "content_type";
    private const string HtmlContentType = "html";
    private const string FrontMatterMarker = "+++";

    private readonly Dictionary<string, Template> _templates = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _templates.Count;

    public void Register(string channelId, string templateId, string source)
    {
        // The mode is chosen from the source because FrontMatterAndContent *requires* the +++ block: a
        // template without one fails to parse under it. Channels with no subject write no front matter,
        // so both shapes have to be accepted.
        var mode = source.TrimStart().StartsWith(FrontMatterMarker, StringComparison.Ordinal)
                       ? ScriptMode.FrontMatterAndContent
                       : ScriptMode.Default;

        var template = Template.Parse(source, templateId, null, new LexerOptions { Mode = mode });

        if (template.HasErrors)
        {
            throw new InvalidDataException(
                $"Notification template '{channelId}/{templateId}' does not compile: " +
                string.Join("; ", template.Messages.Select(message => message.ToString()))
            );
        }

        _templates[Key(channelId, templateId)] = template;
    }

    public NotificationContent? Render(string channelId, string templateId, object model)
    {
        if (!_templates.TryGetValue(Key(channelId, templateId), out var template))
        {
            return null;
        }

        var globals = new ScriptObject();
        globals.Import(model);

        var context = new TemplateContext();
        context.PushGlobal(globals);

        // Evaluated explicitly so the subject is set whether or not rendering the page walks the front
        // matter itself; the assignment is idempotent either way.
        if (template.Page?.FrontMatter is { } frontMatter)
        {
            context.Evaluate(frontMatter);
        }

        var body = template.Render(context);
        var subject = globals.TryGetValue(SubjectVariable, out var value) ? value?.ToString() : null;

        // Anything that is not "html" is text, including a typo: a notification must never be lost to a
        // mistyped piece of metadata.
        var contentType =
            globals.TryGetValue(ContentTypeVariable, out var declared) &&
            string.Equals(declared?.ToString(), HtmlContentType, StringComparison.OrdinalIgnoreCase)
                ? NotificationContentType.Html
                : NotificationContentType.Text;

        return new(subject, body, contentType);
    }

    private static string Key(string channelId, string templateId)
        => $"{channelId}/{templateId}";
}
