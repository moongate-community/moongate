using Moongate.Stress.Internal;

namespace Moongate.Tests.Stress;

public sealed class StressRunOptionsParserTests
{
    [Test]
    public void TryParse_WithDefaults_ShouldSucceed()
    {
        var ok = StressRunOptionsParser.TryParse([], out var options, out var error);

        Assert.Multiple(
            () =>
            {
                Assert.That(ok, Is.True);
                Assert.That(error, Is.Null);
                Assert.That(options.Clients, Is.EqualTo(100));
                Assert.That(options.Duration, Is.EqualTo(TimeSpan.FromMinutes(5)));
                Assert.That(options.RampUpPerSecond, Is.EqualTo(10));
            }
        );
    }

    [Test]
    public void TryParse_WithOverrides_ShouldSucceed()
    {
        var ok = StressRunOptionsParser.TryParse(
            ["--host", "10.0.0.5", "--clients", "250", "--duration", "120", "--verbose"],
            out var options,
            out var error
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(ok, Is.True);
                Assert.That(error, Is.Null);
                Assert.That(options.Host, Is.EqualTo("10.0.0.5"));
                Assert.That(options.Clients, Is.EqualTo(250));
                Assert.That(options.Duration, Is.EqualTo(TimeSpan.FromSeconds(120)));
                Assert.That(options.Verbose, Is.True);
            }
        );
    }

    [Test]
    public void TryParse_WithInvalidClients_ShouldFail()
    {
        var ok = StressRunOptionsParser.TryParse(["--clients", "0"], out _, out var error);

        Assert.Multiple(
            () =>
            {
                Assert.That(ok, Is.False);
                Assert.That(error, Does.Contain("clients"));
            }
        );
    }
}
