namespace Moongate.Abstractions.Data.Email;

/// <summary>
/// Represents a rendered email template payload.
/// </summary>
public sealed class EmailTemplateRenderResult
{
    /// <summary>
    /// Gets or sets the rendered subject.
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the rendered HTML body.
    /// </summary>
    public string HtmlBody { get; set; }

    /// <summary>
    /// Gets or sets the rendered plain text body.
    /// </summary>
    public string TextBody { get; set; }
}
