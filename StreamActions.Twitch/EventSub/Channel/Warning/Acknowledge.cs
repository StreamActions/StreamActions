using System.Text.Json.Serialization;
using StreamActions.Twitch.Api.EventSub;
using StreamActions.Twitch.Api.EventSub.Conditions;

namespace StreamActions.Twitch.EventSub.Channel.Warning;

/// <summary>
/// An event that is sent when Acknowledge.
/// </summary>
public sealed record Acknowledge : IEventSubType
{
    /// <inheritdoc/>
    public static Type EventSubConditionType => typeof(BroadcasterAndModeratorUserIdCondition);

    /// <inheritdoc/>
    public static string Type => "channel.warning.acknowledge";

    /// <inheritdoc/>
    public static string Version => "1";

    /// <summary>
    /// The user ID of the broadcaster.
    /// </summary>
    [JsonPropertyName("broadcaster_user_id")]
    public string? BroadcasterUserId { get; init; }

    /// <summary>
    /// The login of the broadcaster.
    /// </summary>
    [JsonPropertyName("broadcaster_user_login")]
    public string? BroadcasterUserLogin { get; init; }

    /// <summary>
    /// The user name of the broadcaster.
    /// </summary>
    [JsonPropertyName("broadcaster_user_name")]
    public string? BroadcasterUserName { get; init; }

    /// <summary>
    /// The ID of the user that has acknowledged their warning.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// The login of the user that has acknowledged their warning.
    /// </summary>
    [JsonPropertyName("user_login")]
    public string? UserLogin { get; init; }

    /// <summary>
    /// The user name of the user that has acknowledged their warning.
    /// </summary>
    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }

}