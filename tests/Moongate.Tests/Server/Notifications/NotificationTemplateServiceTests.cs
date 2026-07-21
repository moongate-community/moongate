using Moongate.Server.Abstractions.Types;
using Moongate.Server.Services.Notifications;

namespace Moongate.Tests.Server.Notifications;

public sealed class NotificationTemplateServiceTests
{
    [Fact]
    public void Render_TakesTheSubjectFromFrontMatterAndTheBodyFromTheContent()
    {
        var service = new NotificationTemplateService();
        service.Register(
            "email",
            "account_verification",
            """
            +++
            subject = "Verify your " + shard_name + " account"
            +++
            Hello {{ username }}, token {{ token }}.
            """
        );

        var content = service.Render(
            "email",
            "account_verification",
            new { ShardName = "Britannia", Username = "tom", Token = "abc" }
        );

        Assert.Equal("Verify your Britannia account", content!.Subject);
        Assert.Equal("Hello tom, token abc.", content.Body.Trim());

        // The front matter must not leak into the body.
        Assert.DoesNotContain("subject", content.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Render_WithoutFrontMatter_HasNoSubject()
    {
        var service = new NotificationTemplateService();
        service.Register("log", "account_verification", "Token for {{ username }}: {{ token }}");

        var content = service.Render("log", "account_verification", new { Username = "tom", Token = "abc" });

        Assert.Null(content!.Subject);
        Assert.Equal("Token for tom: abc", content.Body.Trim());
    }

    [Fact]
    public void Register_ExposesModelMembersInSnakeCase()
    {
        var service = new NotificationTemplateService();
        service.Register("log", "greet", "{{ shard_name }}");

        var content = service.Render("log", "greet", new { ShardName = "Britannia" });

        Assert.Equal("Britannia", content!.Body.Trim());
    }

    [Fact]
    public void Render_UnknownTemplateOrChannel_IsNull()
    {
        var service = new NotificationTemplateService();
        service.Register("log", "account_verification", "hello");

        Assert.Null(service.Render("log", "nope", new { }));
        Assert.Null(service.Render("email", "account_verification", new { }));
    }

    [Fact]
    public void Register_BrokenSyntax_ThrowsNamingTheTemplate()
    {
        var service = new NotificationTemplateService();

        // Compiling at registration is the whole point: a broken template must fail at load, not on the
        // first notification months later.
        var exception = Assert.Throws<InvalidDataException>(
            () => service.Register("log", "broken", "{{ if }}")
        );

        Assert.Contains("broken", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, service.Count);
    }

    [Fact]
    public void Count_ReportsRegisteredTemplates()
    {
        var service = new NotificationTemplateService();
        service.Register("log", "one", "a");
        service.Register("email", "one", "b");

        Assert.Equal(2, service.Count);
    }

    [Fact]
    public void Render_FrontMatterContentTypeHtml_MarksTheContentAsHtml()
    {
        var service = new NotificationTemplateService();
        service.Register(
            "email",
            "fancy",
            """
            +++
            content_type = "html"
            +++
            <p>Hello {{ username }}</p>
            """
        );

        var content = service.Render("email", "fancy", new { Username = "tom" });

        Assert.Equal(NotificationContentType.Html, content!.ContentType);
        Assert.Equal("<p>Hello tom</p>", content.Body.Trim());
    }

    [Fact]
    public void Render_WithoutContentType_IsText()
    {
        var service = new NotificationTemplateService();
        service.Register("log", "plain", "hello");

        Assert.Equal(NotificationContentType.Text, service.Render("log", "plain", new { })!.ContentType);
    }

    [Fact]
    public void Render_UnknownContentType_FallsBackToText()
    {
        var service = new NotificationTemplateService();
        service.Register(
            "email",
            "typo",
            """
            +++
            content_type = "htlm"
            +++
            hello
            """
        );

        // Deliberately forgiving: a mistyped content type must not cost the notification. Anything that
        // is not "html" is text.
        Assert.Equal(NotificationContentType.Text, service.Render("email", "typo", new { })!.ContentType);
    }
}
