using FluentAssertions;
using Jellyfin.Clips.Data.Entities;
using Xunit;

namespace Jellyfin.Clips.Tests;

public class ClipExtractionTests
{
    [Fact]
    public void Clip_Entity_DefaultValues()
    {
        var clip = new Clip();

        clip.Id.Should().NotBeNullOrEmpty();
        clip.SourceItemId.Should().BeEmpty();
        clip.SourceItemName.Should().BeEmpty();
        clip.FilePath.Should().BeEmpty();
        clip.IsProcessed.Should().BeFalse();
        clip.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void UserInteraction_Entity_DefaultValues()
    {
        var interaction = new UserInteraction();

        interaction.Id.Should().Be(0);
        interaction.UserId.Should().BeEmpty();
        interaction.ClipId.Should().BeEmpty();
        interaction.InteractionType.Should().Be(InteractionType.View);
        interaction.DwellTimeMs.Should().Be(0);
        interaction.CompletionRate.Should().Be(0);
    }

    [Theory]
    [InlineData(InteractionType.View, 0)]
    [InlineData(InteractionType.Like, 1)]
    [InlineData(InteractionType.Dislike, 2)]
    [InlineData(InteractionType.Skip, 3)]
    [InlineData(InteractionType.Rewatch, 4)]
    [InlineData(InteractionType.ClickThrough, 5)]
    [InlineData(InteractionType.Share, 6)]
    public void InteractionType_HasCorrectValues(InteractionType type, int expected)
    {
        ((int)type).Should().Be(expected);
    }

    [Fact]
    public void UserProfile_DefaultValues()
    {
        var profile = new UserProfile();

        profile.UserId.Should().BeEmpty();
        profile.GenrePreferencesJson.Should().Be("{}");
        profile.AvgDwellTimeMs.Should().Be(0);
        profile.TotalInteractions.Should().Be(0);
    }
}
