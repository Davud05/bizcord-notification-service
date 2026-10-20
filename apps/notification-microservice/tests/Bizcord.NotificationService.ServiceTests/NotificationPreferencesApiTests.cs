using System.Net;
using System.Net.Http.Json;
using Bizcord.NotificationService.ServiceTests.Infrastructure;
using Bizcord.Shared.Contracts.Models;

namespace Bizcord.NotificationService.ServiceTests;

public class NotificationPreferencesApiTests : IClassFixture<ServiceTestFactory>
{
    private readonly HttpClient _client;

    public NotificationPreferencesApiTests(ServiceTestFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Get_ReturnsTheDefaults_ForAUserThatNeverChangedAnything()
    {
        var userId = Guid.NewGuid();

        var preferences = await _client.GetFromJsonAsync<NotificationPreferenceDto>(
            $"/api/users/{userId}/notification-preferences");

        preferences.Should().NotBeNull();
        preferences!.UserId.Should().Be(userId);
        preferences.AllowDirectMentions.Should().BeTrue();
        preferences.AllowChannelWideMentions.Should().BeTrue();
        preferences.MutedChannels.Should().BeEmpty();
        preferences.QuietHoursStart.Should().BeNull();
    }

    [Fact]
    public async Task Put_StoresThePreferences()
    {
        var userId = Guid.NewGuid();
        var mutedChannel = Guid.NewGuid();

        var response = await _client.PutAsJsonAsync(
            $"/api/users/{userId}/notification-preferences",
            new
            {
                allowDirectMentions = true,
                allowChannelWideMentions = false,
                mutedChannels = new[] { mutedChannel },
                quietHoursStart = "22:00",
                quietHoursEnd = "07:00"
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var stored = await _client.GetFromJsonAsync<NotificationPreferenceDto>(
            $"/api/users/{userId}/notification-preferences");

        stored!.AllowChannelWideMentions.Should().BeFalse();
        stored.MutedChannels.Should().Equal(mutedChannel);
        stored.QuietHoursStart.Should().Be("22:00");
        stored.QuietHoursEnd.Should().Be("07:00");
    }

    [Fact]
    public async Task Put_Returns400_OnAMalformedQuietHour()
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/users/{Guid.NewGuid()}/notification-preferences",
            new { quietHoursStart = "tonight", quietHoursEnd = "07:00" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("HH:mm");
    }

    [Fact]
    public async Task Put_Returns400_WhenOnlyOneQuietHourIsGiven()
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/users/{Guid.NewGuid()}/notification-preferences",
            new { quietHoursStart = "22:00" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("both a start and an end");
    }
}
