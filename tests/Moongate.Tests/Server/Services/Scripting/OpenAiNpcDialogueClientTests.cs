using Moongate.Server.Data.Config;
using Moongate.Server.Json;
using Moongate.Server.Services.Scripting;
using Moongate.UO.Data.Ids;
using OpenAI.Chat;

namespace Moongate.Tests.Server.Services.Scripting;

public sealed class OpenAiNpcDialogueClientTests
{
    [Test]
    public void BuildDeveloperMessage_ShouldAllowNullMemorySummaryWhenUnchanged()
    {
        var message = OpenAiNpcDialogueClient.BuildDeveloperMessageForTests(
            new()
            {
                NpcId = (Serial)0x100u,
                NpcName = "Lilly",
                Prompt = "You are Lilly.",
                Memory = "[Core Memory]\nLilly remembers Tommy.",
                NearbyPlayerNames = ["Tommy"]
            }
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(message, Does.Contain("memory_summary may be null if no long-term memory change is needed"));
                Assert.That(message, Does.Not.Contain("memory_summary must contain the full compact memory summary"));
            }
        );
    }

    [Test]
    public void DescribeContentParts_ShouldReportKindAndLengths()
    {
        IReadOnlyList<ChatMessageContentPart> parts =
        [
            ChatMessageContentPart.CreateTextPart("{\"ok\":true}"),
            ChatMessageContentPart.CreateRefusalPart("blocked")
        ];

        var description = OpenAiNpcDialogueClient.DescribeContentParts(parts);

        Assert.That(description, Is.EqualTo("[0]Text:text=11:refusal=0, [1]Refusal:text=0:refusal=7"));
    }

    [Test]
    public void DeserializeResponse_ShouldUseSourceGeneratedJsonContext()
    {
        const string json = """
                            {
                              "should_speak": true,
                              "speech_text": "Hello, traveler.",
                              "memory_summary": "Lilly met Tomy.",
                              "mood": "warm"
                            }
                            """;

        var response = OpenAiNpcDialogueClient.DeserializeResponseForTests(json);

        Assert.Multiple(
            () =>
            {
                Assert.That(response, Is.Not.Null);
                Assert.That(response!.ShouldSpeak, Is.True);
                Assert.That(response.SpeechText, Is.EqualTo("Hello, traveler."));
                Assert.That(response.MemorySummary, Is.EqualTo("Lilly met Tomy."));
                Assert.That(response.Mood, Is.EqualTo("warm"));
                Assert.That(MoongateServerJsonContext.Default.NpcDialogueResponse, Is.Not.Null);
            }
        );
    }

    [Test]
    public void ExtractContentText_ShouldConcatenateOnlyTextParts()
    {
        IReadOnlyList<ChatMessageContentPart> parts =
        [
            ChatMessageContentPart.CreateRefusalPart("nope"),
            ChatMessageContentPart.CreateTextPart("{\"should_speak\":"),
            ChatMessageContentPart.CreateTextPart("true}")
        ];

        var contentText = OpenAiNpcDialogueClient.ExtractContentText(parts);

        Assert.That(contentText, Is.EqualTo("{\"should_speak\":true}"));
    }

    [Test]
    public void ExtractRefusalText_ShouldPreferTopLevelRefusalAndFallbackToRefusalParts()
    {
        IReadOnlyList<ChatMessageContentPart> parts =
        [
            ChatMessageContentPart.CreateRefusalPart("content refusal")
        ];

        var topLevel = OpenAiNpcDialogueClient.ExtractRefusalText("top refusal", parts);
        var contentPartFallback = OpenAiNpcDialogueClient.ExtractRefusalText(string.Empty, parts);

        Assert.Multiple(
            () =>
            {
                Assert.That(topLevel, Is.EqualTo("top refusal"));
                Assert.That(contentPartFallback, Is.EqualTo("content refusal"));
            }
        );
    }

    [Test]
    public void LlmConfig_ShouldDefaultToHigherOutputTokenBudget()
    {
        var config = new MoongateLlmConfig();

        Assert.That(config.MaxOutputTokenCount, Is.EqualTo(1200));
    }
}
