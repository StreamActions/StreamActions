/*
 * This file is part of StreamActions.
 * Copyright © 2019-2024 StreamActions Team (streamactions.github.io)
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

using StreamActions.Common.Json.Serialization;
using StreamActions.Twitch.Api.EventSub.Conditions;
using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;

namespace StreamActions.Twitch.Api.EventSub;

/// <summary>
/// Converts JSON data to an <see cref="EventSubSubscription"/>, ensuring that <see cref="EventSubSubscription.Condition"/> is decoded as the correct type.
/// </summary>
public sealed class EventSubSubscriptionConverter : JsonCustomConverter<EventSubSubscription>
{
    #region Protected Methods

    protected override void Read(ref Utf8JsonReader reader, Utf8JsonReader newReader, Type typeToConvert, JsonSerializerOptions options, EventSubSubscription obj, string propertyName, string jsonPropertyName)
    {
        if (obj is null)
        {
            return;
        }

        if (propertyName is nameof(EventSubSubscription.Condition))
        {
            string? subscriptionType = obj.Type;

            if (string.IsNullOrWhiteSpace(subscriptionType))
            {
                string typeJsonPropertyName = this.MapToJsonPropertyNames().GetValueOrDefault(nameof(EventSubSubscription.Type), nameof(EventSubSubscription.Type));
                while (newReader.Read())
                {
                    if (newReader.TokenType is JsonTokenType.PropertyName && newReader.GetString()! == typeJsonPropertyName)
                    {
                        if (newReader.Read() && newReader.TokenType is JsonTokenType.String)
                        {
                            subscriptionType = reader.GetString();
                            break;
                        }
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(subscriptionType) && _subscriptionTypesToConditions.TryGetValue(subscriptionType, out Type? conditionType))
            {
                this.SetMember(obj, propertyName, JsonSerializer.Deserialize(ref reader, conditionType, options));
            }
        }
    }

    protected override bool ShouldUseDefaultConversion(string propertyName, string jsonPropertyName) => propertyName is not nameof(EventSubSubscription.Condition) && base.ShouldUseDefaultConversion(propertyName, jsonPropertyName);

    protected override void Write(Utf8JsonWriter writer, EventSubSubscription obj, JsonSerializerOptions options, string propertyName, string jsonPropertyName, object? value) => this.DefaultWrite(writer, jsonPropertyName, value, options);

    #endregion Protected Methods

    #region Private Fields

    /// <summary>
    /// Map of <see cref="EventSubSubscription.Type"/> to <see cref="EventSubCondition"/>.
    /// </summary>
    private static readonly ImmutableDictionary<string, Type> _subscriptionTypesToConditions = new Func<ImmutableDictionary<string, Type>>(() =>
    {
        Dictionary<string, Type> subscriptionTypesToConditions = [];

        foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
        {
            if (typeof(IEventSubType).IsAssignableFrom(type) && type.IsClass && !type.IsAbstract)
            {
                string? eventSubType = (string?)type.GetProperty(nameof(IEventSubType.Type), BindingFlags.Static)?.GetValue(null);
                Type? eventSubConditionType = (Type?)type.GetProperty(nameof(IEventSubType.EventSubConditionType))?.GetValue(null);

                if (eventSubType is not null && eventSubConditionType is not null)
                {
                    subscriptionTypesToConditions.Add(eventSubType, eventSubConditionType);
                }
            }
        }

        return subscriptionTypesToConditions.ToImmutableDictionary();
    })();

    #endregion Private Fields
}
