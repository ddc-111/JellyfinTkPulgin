using FluentAssertions;
using Jellyfin.Clips.Model;
using Xunit;

namespace Jellyfin.Clips.Tests;

public class RecommendationEngineTests
{
    [Fact]
    public void ClipDto_DefaultValues_AreCorrect()
    {
        var dto = new ClipDto();

        dto.Id.Should().BeEmpty();
        dto.SourceItemId.Should().BeEmpty();
        dto.SourceItemName.Should().BeEmpty();
        dto.LikeCount.Should().Be(0);
        dto.AvgCompletionRate.Should().Be(0);
        dto.HasLiked.Should().BeFalse();
    }

    [Fact]
    public void FeedResponse_DefaultValues_AreCorrect()
    {
        var response = new FeedResponse();

        response.Clips.Should().BeEmpty();
        response.NextCursor.Should().BeNull();
        response.TotalAvailable.Should().Be(0);
    }

    [Theory]
    [InlineData(10, 20)]
    [InlineData(60, 120)]
    public void FeedRequest_DefaultCount_IsValid(int count, int expectedMax)
    {
        var request = new FeedRequest { Count = count };

        request.Count.Should().BeLessThanOrEqualTo(expectedMax);
        request.Cursor.Should().BeNull();
        request.Genre.Should().BeNull();
    }
}
