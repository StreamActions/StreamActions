/*
 * This file is part of StreamActions.
 * Copyright © 2019-2023 StreamActions Team (streamactions.github.io)
 *
 * StreamActions is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * StreamActions is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with StreamActions.  If not, see <https://www.gnu.org/licenses/>.
 */

using StreamActions.Common;
using StreamActions.Common.Logger;
using StreamActions.Twitch.Api.Common;
using StreamActions.Twitch.Exceptions;
using StreamActions.Twitch.OAuth;
using System.Globalization;
using StreamActions.Twitch.Extensions;
using System.Text.Json.Serialization;
using System.Drawing;
using System.Net.Http.Json;

namespace StreamActions.Twitch.Api.ChannelPoints;

/// <summary>
/// Sends and represents a response element for a request to create, edit, delete, or get channel points rewards.
/// </summary>
public sealed record ChannelPointsReward
{
    /// <summary>
    /// User ID of the broadcaster.
    /// </summary>
    [JsonPropertyName("broadcaster_id")]
    public string? BroadcasterId { get; init; }

    /// <summary>
    /// Login of the broadcaster.
    /// </summary>
    [JsonPropertyName("broadcaster_login")]
    public string? BroadcasterLogin { get; init; }

    /// <summary>
    /// Display name of the broadcaster.
    /// </summary>
    [JsonPropertyName("broadcaster_name")]
    public string? BroadcasterName { get; init; }

    /// <summary>
    /// The ID that uniquely identifies this custom reward.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid? Id { get; init; }

    /// <summary>
    /// A set of custom images for the reward. This field is <see langword="null"/> if the broadcaster didn't upload images.
    /// </summary>
    [JsonPropertyName("image")]
    public Images? Image { get; init; }

    /// <summary>
    /// The background color to use for the reward, as a string. The color is in Hex format (for example, <c>#00E5CB</c>).
    /// </summary>
    [JsonPropertyName("background_color")]
    public string? BackgroundColorString { get; init; }

    /// <summary>
    /// The background color to use for the reward.
    /// </summary>
    [JsonIgnore]
    public Color? BackgroundColor => Util.IsValidHexColor(this.BackgroundColorString ?? "") ? Util.HexColorToColor(this.BackgroundColorString ?? "") : null;

    /// <summary>
    /// A Boolean value that determines whether the reward is enabled. Disabled rewards aren't shown to the user.
    /// </summary>
    [JsonPropertyName("is_enabled")]
    public bool? IsEnabled { get; init; }

    /// <summary>
    /// The cost of the reward in Channel Points.
    /// </summary>
    [JsonPropertyName("cost")]
    public int? Cost { get; init; }

    /// <summary>
    /// The title of the reward.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// The prompt/description shown to the viewer when they redeem the reward.
    /// </summary>
    [JsonPropertyName("prompt")]
    public string? Prompt { get; init; }

    /// <summary>
    /// A Boolean value that determines whether the user must enter information when redeeming the reward.
    /// </summary>
    [JsonPropertyName("is_user_input_required")]
    public bool? IsUserInputRequired { get; init; }

    /// <summary>
    /// The settings used to determine whether to apply a maximum to the number of redemptions allowed per live stream.
    /// </summary>
    [JsonPropertyName("max_per_stream_setting")]
    public ChannelPointsMaxPerStreamSetting? MaxPerStreamSetting { get; init; }

    /// <summary>
    /// The settings used to determine whether to apply a maximum to the number of redemptions allowed per user per live stream.
    /// </summary>
    [JsonPropertyName("max_per_user_per_stream_setting")]
    public ChannelPointsMaxPerUserPerStreamSetting? MaxPerUserPerStreamSetting { get; init; }

    /// <summary>
    /// The settings used to determine whether to apply a cooldown period between redemptions and the length of the cooldown.
    /// </summary>
    [JsonPropertyName("global_cooldown_setting")]
    public ChannelPointsGlobalCooldownSetting? GlobalCooldownSetting { get; init; }

    /// <summary>
    /// A Boolean value that determines whether the reward is currently paused. Viewers can't redeem paused rewards.
    /// </summary>
    [JsonPropertyName("is_paused")]
    public bool? IsPaused { get; init; }

    /// <summary>
    /// A Boolean value that determines whether the reward is currently in stock. Viewers can't redeem out of stock rewards.
    /// </summary>
    [JsonPropertyName("is_in_stock")]
    public bool? IsInStock { get; init; }

    /// <summary>
    /// A set of default images for the reward.
    /// </summary>
    [JsonPropertyName("default_image")]
    public Images? DefaultImage { get; init; }

    /// <summary>
    /// A Boolean value that determines whether redemptions should be set to <c>FULFILLED</c> status immediately when a reward is redeemed.
    /// If <see langword="false"/>, status is set to <c>UNFULFILLED</c> and follows the normal request queue process.
    /// </summary>
    [JsonPropertyName("should_redemptions_skip_request_queue")]
    public bool? ShouldRedemptionsSkipRequestQueue { get; init; }

    /// <summary>
    /// The number of redemptions redeemed during the current live stream.
    /// The number counts against the <see cref="MaxPerStreamSetting"/> limit.
    /// This field is <see langword="null"/> if the broadcaster's stream isn't live or <see cref="ChannelPointsMaxPerStreamSetting.IsEnabled"/> is <see langword="false"/>.
    /// </summary>
    [JsonPropertyName("redemptions_redeemed_current_stream")]
    public int? RedemptionsRedeemedCurrentStream { get; init; }

    /// <summary>
    /// The timestamp of when the cooldown period expires. Is <see langword="null"/> if the reward isn't in a cooldown state.
    /// </summary>
    [JsonPropertyName("cooldown_expires_at")]
    public DateTime? CooldownExpiresAt { get; init; }

    /// <summary>
    /// Returns a list of Custom Reward objects for the Custom Rewards on a channel.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster whose custom rewards you want to get. This ID must match the user ID found in the OAuth token.</param>
    /// <param name="id">A list of IDs to filter the rewards by. You may specify a maximum of 50 IDs.</param>
    /// <param name="onlyManageableRewards">A Boolean value that determines whether the response contains only the custom rewards that the app may manage (the app is identified by the ID in the Client-Id header). Set to <see langword="true"/> to get only the custom rewards that the app may manage. The default is <see langword="false"/>.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="ChannelPointsReward"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is null; <paramref name="broadcasterId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is defined and has more than 50 elements.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelReadRedemptions"/>.</exception>
    /// <remarks>
    /// <para>
    /// There is a limit of 50 Custom Rewards on a channel at a time. This includes both enabled and disabled Custom Rewards.
    /// </para>
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "API Definition")]
    public static async Task<ResponseData<ChannelPointsReward>?> GetCustomReward(TwitchSession session, string broadcasterId, IEnumerable<Guid>? id = null, bool? onlyManageableRewards = null)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (id is not null && id.Count() > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(id), id.Count(), "must have a count <= 50").Log(TwitchApi.GetLogger());
        }

        session.RequireToken(Scope.ChannelReadRedemptions);

        Dictionary<string, IEnumerable<string>> queryParams = new()
        {
            { "broadcaster_id", new List<string> { broadcasterId } }
        };

        if (id is not null && id.Any())
        {
            queryParams.Add("id", id.Select(x => x.ToString("D", CultureInfo.InvariantCulture)));
        }

        if (onlyManageableRewards.HasValue)
        {
            queryParams.Add("only_manageable_rewards", new List<string> { onlyManageableRewards.Value.ToString(CultureInfo.InvariantCulture).ToLowerInvariant() });
        }

        Uri uri = Util.BuildUri(new("/channel_points/custom_rewards"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Get, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<ChannelPointsReward>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a Custom Reward in the broadcaster's channel.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster to add the custom reward to. This ID must match the user ID found in the OAuth token.</param>
    /// <param name="parameters">The custom reward to create.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="ChannelPointsReward"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/>, <paramref name="parameters"/>, or <see cref="ChannelPointsRewardCreationParameters.Cost"/> is null; <paramref name="broadcasterId"/> or <see cref="ChannelPointsRewardCreationParameters.Title"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="ChannelPointsRewardCreationParameters.Cost"/> is < 1; <see cref="ChannelPointsRewardCreationParameters.Title"/> has more than 45 characters; <see cref="ChannelPointsRewardCreationParameters.Prompt"/> has more than 200 characters.</exception>
    /// <exception cref="InvalidOperationException"><see cref="ChannelPointsRewardCreationParameters.BackgroundColor"/> is not a valid hex triplet in the form <c>#RRGGBB</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="ChannelPointsRewardCreationParameters.MaxPerStream"/>, <see cref="ChannelPointsRewardCreationParameters.MaxPerUserPerStream"/>, or <see cref="ChannelPointsRewardCreationParameters.GlobalCooldownSeconds"/> is enabled and < 1.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelManageRedemptions"/>.</exception>
    /// <remarks>
    /// <para>
    /// There is a limit of 50 Custom Rewards on a channel at a time. This includes both enabled and disabled Custom Rewards.
    /// </para>
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly", Justification = "Intentional")]
    public static async Task<ResponseData<ChannelPointsReward>?> CreateCustomReward(TwitchSession session, string broadcasterId, ChannelPointsRewardCreationParameters parameters)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(parameters.Title))
        {
            throw new ArgumentNullException(nameof(parameters.Title)).Log(TwitchApi.GetLogger());
        }

        if (parameters.Title.Length > 45)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters.Title), parameters.Title.Length, "length must be <= 45").Log(TwitchApi.GetLogger());
        }

        if (!parameters.Cost.HasValue)
        {
            throw new ArgumentNullException(nameof(parameters.Cost)).Log(TwitchApi.GetLogger());
        }

        if (parameters.Cost.Value < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters.Cost), parameters.Cost.Value, "must be >= 1").Log(TwitchApi.GetLogger());
        }

        session.RequireToken(Scope.ChannelManageRedemptions);

        if (!string.IsNullOrWhiteSpace(parameters.Prompt) && parameters.Prompt.Length > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters.Prompt), parameters.Prompt.Length, "length must be <= 200").Log(TwitchApi.GetLogger());
        }

        if (!string.IsNullOrWhiteSpace(parameters.BackgroundColor) && !Util.IsValidHexColor(parameters.BackgroundColor))
        {
            throw new InvalidOperationException(nameof(parameters.BackgroundColor) + " must be a valid hex triplet (#RRGGBB)").Log(TwitchApi.GetLogger());
        }

        if (parameters.IsMaxPerStreamEnabled.GetValueOrDefault(false) && parameters.MaxPerStream.GetValueOrDefault(0) < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters.MaxPerStream), parameters.MaxPerStream.GetValueOrDefault(0), "must be >= 1").Log(TwitchApi.GetLogger());
        }

        if (parameters.IsMaxPerUserPerStreamEnabled.GetValueOrDefault(false) && parameters.MaxPerUserPerStream.GetValueOrDefault(0) < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters.MaxPerUserPerStream), parameters.MaxPerUserPerStream.GetValueOrDefault(0), "must be >= 1").Log(TwitchApi.GetLogger());
        }

        if (parameters.IsGlobalCooldownEnabled.GetValueOrDefault(false) && parameters.GlobalCooldownSeconds.GetValueOrDefault(0) < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters.GlobalCooldownSeconds), parameters.GlobalCooldownSeconds.GetValueOrDefault(0), "must be >= 1").Log(TwitchApi.GetLogger());
        }

        Dictionary<string, IEnumerable<string>> queryParams = new()
        {
            { "broadcaster_id", new List<string> { broadcasterId } }
        };

        using JsonContent content = JsonContent.Create(parameters, options: TwitchApi.SerializerOptions);
        Uri uri = Util.BuildUri(new("/channel_points/custom_rewards"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Post, uri, session, content).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<ChannelPointsReward>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates a custom reward.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster to add the custom reward to. This ID must match the user ID found in the OAuth token.</param>
    /// <param name="parameters">The custom reward to create.</param>
    /// <returns>A <see cref="ResponseData{TDataType}"/> with elements of type <see cref="ChannelPointsReward"/> containing the response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="parameters"/> is null; <paramref name="id"/> is <see cref="Guid.Empty"/>; <paramref name="broadcasterId"/> is <see langword="null"/>, empty, or whitespace; <see cref="ChannelPointsRewardCreationParameters.Title"/> is not <see langword="null"/> and is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="ChannelPointsRewardCreationParameters.Cost"/> is < 1; <see cref="ChannelPointsRewardCreationParameters.Title"/> has more than 45 characters; <see cref="ChannelPointsRewardCreationParameters.Prompt"/> has more than 200 characters.</exception>
    /// <exception cref="InvalidOperationException">No parameters were set in <paramref name="parameters"/>; <see cref="ChannelPointsRewardCreationParameters.BackgroundColor"/> is not a valid hex triplet in the form <c>#RRGGBB</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="ChannelPointsRewardCreationParameters.MaxPerStream"/>, <see cref="ChannelPointsRewardCreationParameters.MaxPerUserPerStream"/>, or <see cref="ChannelPointsRewardCreationParameters.GlobalCooldownSeconds"/> is < 1.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelManageRedemptions"/>.</exception>
    /// <remarks>
    /// <para>
    /// The app used to create the reward is the only app that may update the reward.
    /// </para>
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly", Justification = "Intentional")]
    public static async Task<ResponseData<ChannelPointsReward>?> UpdateCustomReward(TwitchSession session, string broadcasterId, Guid id, ChannelPointsRewardUpdateParameters parameters)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (id == Guid.Empty)
        {
            throw new ArgumentNullException(nameof(id)).Log(TwitchApi.GetLogger());
        }

        if (parameters is null)
        {
            throw new ArgumentNullException(nameof(parameters)).Log(TwitchApi.GetLogger());
        }

        session.RequireToken(Scope.ChannelManageRedemptions);

        if (parameters.Title is not null && string.IsNullOrWhiteSpace(parameters.Title))
        {
            throw new ArgumentNullException(nameof(parameters.Title)).Log(TwitchApi.GetLogger());
        }

        if (parameters.Title is not null && parameters.Title.Length > 45)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters.Title), parameters.Title.Length, "length must be <= 45").Log(TwitchApi.GetLogger());
        }

        if (parameters.Cost.HasValue && parameters.Cost.Value < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters.Cost), parameters.Cost.Value, "must be >= 1").Log(TwitchApi.GetLogger());
        }

        if (!string.IsNullOrWhiteSpace(parameters.Prompt) && parameters.Prompt.Length > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters.Prompt), parameters.Prompt.Length, "length must be <= 200").Log(TwitchApi.GetLogger());
        }

        if (!string.IsNullOrWhiteSpace(parameters.BackgroundColor) && !Util.IsValidHexColor(parameters.BackgroundColor))
        {
            throw new InvalidOperationException(nameof(parameters.BackgroundColor) + " must be a valid hex triplet (#RRGGBB)").Log(TwitchApi.GetLogger());
        }

        if (parameters.MaxPerStream.HasValue && parameters.MaxPerStream.Value < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters.MaxPerStream), parameters.MaxPerStream.GetValueOrDefault(0), "must be >= 1").Log(TwitchApi.GetLogger());
        }

        if (parameters.MaxPerUserPerStream.HasValue && parameters.MaxPerUserPerStream.Value < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters.MaxPerUserPerStream), parameters.MaxPerUserPerStream.GetValueOrDefault(0), "must be >= 1").Log(TwitchApi.GetLogger());
        }

        if (parameters.GlobalCooldownSeconds.HasValue && parameters.GlobalCooldownSeconds.Value < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters.GlobalCooldownSeconds), parameters.GlobalCooldownSeconds.GetValueOrDefault(0), "must be >= 1").Log(TwitchApi.GetLogger());
        }

        Dictionary<string, IEnumerable<string>> queryParams = new()
        {
            { "broadcaster_id", new List<string> { broadcasterId } },
            { "id", new List<string> { id.ToString("D", CultureInfo.InvariantCulture) } }
        };

        if (!(parameters.BackgroundColor is not null || parameters.IsEnabled.HasValue || parameters.Cost.HasValue || parameters.Title is not null || parameters.Prompt is not null
            || parameters.IsPaused.HasValue || parameters.IsUserInputRequired.HasValue || parameters.IsMaxPerStreamEnabled.HasValue || parameters.MaxPerStream.HasValue
            || parameters.IsMaxPerUserPerStreamEnabled.HasValue || parameters.MaxPerUserPerStream.HasValue || parameters.IsGlobalCooldownEnabled.HasValue
            || parameters.GlobalCooldownSeconds.HasValue || parameters.ShouldRedemptionsSkipRequestQueue.HasValue))
        {
            throw new InvalidOperationException("must specify at least one parameter to update").Log(TwitchApi.GetLogger());
        }

        using JsonContent content = JsonContent.Create(parameters, options: TwitchApi.SerializerOptions);
        Uri uri = Util.BuildUri(new("/channel_points/custom_rewards"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Patch, uri, session, content).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<ResponseData<ChannelPointsReward>>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes a custom reward that the broadcaster created.
    /// </summary>
    /// <param name="session">The <see cref="TwitchSession"/> to authorize the request.</param>
    /// <param name="broadcasterId">The ID of the broadcaster that created the custom reward. This ID must match the user ID found in the OAuth token.</param>
    /// <param name="id">The ID of the custom reward to delete.</param>
    /// <returns>A <see cref="TwitchResponse"/> containing the response code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is null; <paramref name="broadcasterId"/> is <see langword="null"/>, empty, or whitespace; <paramref name="id"/> is <see cref="Guid.Empty"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="TwitchSession.Token"/> is <see langword="null"/>; <see cref="TwitchToken.OAuth"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="TwitchScopeMissingException"><paramref name="session"/> does not have the scope <see cref="Scope.ChannelManageRedemptions"/>.</exception>
    /// <remarks>
    /// <para>
    /// The app used to create the reward is the only app that may delete it.
    /// </para>
    /// <para>
    /// If the reward has any redemptions with status <c>UNFULFILLED</c> at the time the reward is deleted, the redemption statuses are marked as <c>FULFILLED</c>.
    /// </para>
    /// </remarks>
    public static async Task<TwitchResponse?> DeleteCustomReward(TwitchSession session, string broadcasterId, Guid id)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session)).Log(TwitchApi.GetLogger());
        }

        if (string.IsNullOrWhiteSpace(broadcasterId))
        {
            throw new ArgumentNullException(nameof(broadcasterId)).Log(TwitchApi.GetLogger());
        }

        if (id == Guid.Empty)
        {
            throw new ArgumentNullException(nameof(id)).Log(TwitchApi.GetLogger());
        }

        session.RequireToken(Scope.ChannelManageRedemptions);

        Dictionary<string, IEnumerable<string>> queryParams = new()
        {
            { "broadcaster_id", new List<string> { broadcasterId } },
            { "id", new List<string> { id.ToString("D", CultureInfo.InvariantCulture) } }
        };

        Uri uri = Util.BuildUri(new("/channel_points/custom_rewards"), queryParams);
        HttpResponseMessage response = await TwitchApi.PerformHttpRequest(HttpMethod.Delete, uri, session).ConfigureAwait(false);
        return await response.ReadFromJsonAsync<TwitchResponse>(TwitchApi.SerializerOptions).ConfigureAwait(false);
    }
}
