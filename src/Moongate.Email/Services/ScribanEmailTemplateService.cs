using Moongate.Abstractions.Data.Email;
using Moongate.Abstractions.Interfaces.Services.Email;
using Moongate.Abstractions.Services.Base;
using Moongate.Email.Data;
using Scriban;
using Scriban.Runtime;

namespace Moongate.Email.Services;

/// <summary>
/// Renders email templates from disk using Scriban.
/// </summary>
public sealed class ScribanEmailTemplateService : BaseMoongateService, IEmailTemplateService
{
    private readonly EmailTemplateOptions _options;

    public ScribanEmailTemplateService(EmailTemplateOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task<EmailTemplateRenderResult> RenderAsync(
        EmailTemplateRenderRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(_options.TemplatesRootPath))
        {
            throw new InvalidOperationException("Email template root path is not configured.");
        }

        var locale = string.IsNullOrWhiteSpace(request.Locale) ? _options.FallbackLocale : request.Locale;
        var subjectPath = ResolveTemplatePath(request.TemplateId, locale, "subject.sbn");
        var htmlPath = ResolveTemplatePath(request.TemplateId, locale, "html.sbn");
        var textPath = ResolveTemplatePath(request.TemplateId, locale, "text.sbn");

        var result = new EmailTemplateRenderResult
        {
            Subject = await RenderTemplateFileAsync(subjectPath, request.Model, cancellationToken),
            HtmlBody = await RenderTemplateFileAsync(htmlPath, request.Model, cancellationToken),
            TextBody = await RenderTemplateFileAsync(textPath, request.Model, cancellationToken)
        };

        return result;
    }

    private string ResolveTemplatePath(string templateId, string locale, string suffix)
    {
        if (string.IsNullOrWhiteSpace(templateId))
        {
            throw new ArgumentException("Template id is required.", nameof(templateId));
        }

        var localePath = Path.Combine(_options.TemplatesRootPath, templateId, $"{locale}.{suffix}");

        if (File.Exists(localePath))
        {
            return localePath;
        }

        var fallbackPath = Path.Combine(_options.TemplatesRootPath, templateId, $"{_options.FallbackLocale}.{suffix}");

        if (File.Exists(fallbackPath))
        {
            return fallbackPath;
        }

        throw new FileNotFoundException(
            $"Email template file not found for '{templateId}' and suffix '{suffix}'.",
            localePath
        );
    }

    private static async Task<string> RenderTemplateFileAsync(
        string path,
        IReadOnlyDictionary<string, object?> model,
        CancellationToken cancellationToken
    )
    {
        var source = await File.ReadAllTextAsync(path, cancellationToken);
        var template = Template.Parse(source, path);

        if (template.HasErrors)
        {
            var firstError = template.Messages.Count > 0
                                 ? template.Messages[0].Message
                                 : "unknown parse error";
            throw new InvalidOperationException($"Invalid email template '{path}': {firstError}");
        }

        var scriptObject = new ScriptObject();
        foreach (var (key, value) in model)
        {
            scriptObject[key] = value;
        }
        var context = new TemplateContext();
        context.PushGlobal(scriptObject);

        return await template.RenderAsync(context);
    }
}
