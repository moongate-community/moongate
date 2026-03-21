using Moongate.UO.Data.Data.Reputation;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;

namespace Moongate.Tests.UO.Data.Utils;

public class ReputationTitleFormatterTests
{
    [SetUp]
    public void SetUp()
    {
        ReputationTitleRuntime.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        ReputationTitleRuntime.Reset();
    }

    [Test]
    public void FormatDisplayName_WhenMobileHasNeutralReputation_ShouldReturnNameOnly()
    {
        var mobile = CreateMobile();

        var displayName = ReputationTitleFormatter.FormatDisplayName(mobile);

        Assert.That(displayName, Is.EqualTo("Marcus"));
    }

    [Test]
    public void FormatDisplayName_WhenMobileHasPositiveReputation_ShouldIncludeReputationPrefix()
    {
        var mobile = CreateMobile();
        mobile.Fame = 3000;
        mobile.Karma = 10000;

        var displayName = ReputationTitleFormatter.FormatDisplayName(mobile);

        Assert.That(displayName, Is.EqualTo("The Great Marcus"));
    }

    [Test]
    public void FormatDisplayName_WhenMobileHasLegendaryFame_ShouldIncludeHonorific()
    {
        var mobile = CreateMobile();
        mobile.Fame = 10000;
        mobile.Karma = 10000;

        var displayName = ReputationTitleFormatter.FormatDisplayName(mobile);

        Assert.That(displayName, Is.EqualTo("The Glorious Lord Marcus"));
    }

    [Test]
    public void FormatDisplayName_WhenMobileIsLegendaryFemaleWithNegativeKarma_ShouldIncludeLadyHonorific()
    {
        var mobile = CreateMobile();
        mobile.Name = "Minax";
        mobile.Gender = GenderType.Female;
        mobile.Fame = 10000;
        mobile.Karma = -2500;

        var displayName = ReputationTitleFormatter.FormatDisplayName(mobile);

        Assert.That(displayName, Is.EqualTo("The Dark Lady Minax"));
    }

    [Test]
    public void FormatDisplayName_WhenMobileHasCustomTitle_ShouldAppendItAfterName()
    {
        var mobile = CreateMobile();
        mobile.Fame = 3000;
        mobile.Karma = 10000;
        mobile.Title = "the brave";

        var displayName = ReputationTitleFormatter.FormatDisplayName(mobile);

        Assert.That(displayName, Is.EqualTo("The Great Marcus the brave"));
    }

    [Test]
    public void FormatDisplayName_WhenRuntimeConfigurationOverridesDefaultTitles_ShouldUseConfiguredTitles()
    {
        ReputationTitleRuntime.Configure(
            ReputationTitleConfiguration.Create(
                new("Baron", "Baroness"),
                [
                    new(
                        10000,
                        [
                            new(10000, "The Custom")
                        ]
                    )
                ]
            )
        );

        var mobile = CreateMobile();
        mobile.Fame = 10000;
        mobile.Karma = 10000;

        var displayName = ReputationTitleFormatter.FormatDisplayName(mobile);

        Assert.That(displayName, Is.EqualTo("The Custom Baron Marcus"));
    }

    private static UOMobileEntity CreateMobile()
    {
        return new()
        {
            Name = "Marcus",
            Gender = GenderType.Male
        };
    }
}
