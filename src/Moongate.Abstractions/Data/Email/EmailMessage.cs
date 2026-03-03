namespace Moongate.Abstractions.Data.Email;

/// <summary>
/// Represents a fully rendered email message ready to send.
/// </summary>
public sealed class EmailMessage
{
    /// <summary>
    /// Gets or sets the message identifier.
    /// </summary>
    public string MessageId { get; set; }

    /// <summary>
    /// Gets or sets the recipient email address.
    /// </summary>
    public string To { get; set; }

    /// <summary>
    /// Gets or sets the sender email address.
    /// </summary>
    public string From { get; set; }

    /// <summary>
    /// Gets or sets the message subject.
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the HTML body.
    /// </summary>
    public string HtmlBody { get; set; }

    /// <summary>
    /// Gets or sets the plain text body.
    /// </summary>
    public string TextBody { get; set; }

    /// <summary>
    /// Gets or sets the template identifier used to render the message.
    /// </summary>
    public string TemplateId { get; set; }

    /// <summary>
    /// Gets or sets the template locale used to render the message.
    /// </summary>
    public string Locale { get; set; } = "en";
}
