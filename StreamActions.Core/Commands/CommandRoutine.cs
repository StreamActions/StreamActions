/*
 * This file is part of StreamActions.
 * Copyright © 2019-2026 StreamActions Team (streamactions.github.io)
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

using System.Text.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Core.Commands;

/// <summary>
/// Base type for polymorphic action steps stored in a <see cref="CommandRoutine"/>, compatible with Milestone 10 Tasker action blocks.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(ChatSendActionStep), ChatSendActionStep.StepType)]
public abstract record CommandRoutineStep;

/// <summary>
/// Tier 1 action step that sends a chat message (<c>chat.send</c>).
/// </summary>
/// <param name="Message">The template or literal chat message text to send.</param>
public sealed record ChatSendActionStep(
    [property: JsonPropertyName("message")] string Message) : CommandRoutineStep
{
    /// <summary>
    /// Discriminator identifier for the <c>chat.send</c> action step.
    /// </summary>
    public const string StepType = "chat.send";
}

/// <summary>
/// Preserves forward-compatible action steps whose <c>$type</c> discriminator is not yet handled in Tier 1.
/// </summary>
/// <param name="StepType">The raw <c>$type</c> discriminator value.</param>
/// <param name="RawJson">The raw JSON representation of the step.</param>
public sealed record UnknownCommandRoutineStep(string StepType, string RawJson) : CommandRoutineStep;

/// <summary>
/// Represents a parsed command action routine supporting Tier 1 JSON (<c>{"steps":[{"$type":"chat.send","message":"..."}]}</c>) and plain-text fallback.
/// </summary>
public sealed class CommandRoutine
{
    /// <summary>
    /// Shared JSON serializer options configured for polymorphic <see cref="CommandRoutineStep"/> serialization.
    /// </summary>
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowOutOfOrderMetadataProperties = true,
    };

    /// <summary>
    /// Gets an empty <see cref="CommandRoutine"/> containing zero steps.
    /// </summary>
    public static CommandRoutine Empty { get; } = new([]);

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandRoutine"/> class.
    /// </summary>
    /// <param name="steps">The ordered sequence of action steps in this routine.</param>
    [JsonConstructor]
    public CommandRoutine(IReadOnlyList<CommandRoutineStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        this.Steps = steps;
    }

    /// <summary>
    /// Gets the ordered sequence of action steps in this routine.
    /// </summary>
    [JsonPropertyName("steps")]
    public IReadOnlyList<CommandRoutineStep> Steps { get; }

    /// <summary>
    /// Creates a <see cref="CommandRoutine"/> containing a single <see cref="ChatSendActionStep"/> for plain-text output.
    /// </summary>
    /// <param name="message">The plain-text chat message.</param>
    /// <returns>A <see cref="CommandRoutine"/> emitting <paramref name="message"/>, or <see cref="Empty"/> if blank.</returns>
    public static CommandRoutine FromPlainText(string? message) =>
        string.IsNullOrWhiteSpace(message)
            ? Empty
            : new CommandRoutine([new ChatSendActionStep(message.Trim())]);

    /// <summary>
    /// Parses either Tier 1 routine JSON (<c>{"steps":[...]}</c>) or plain-text fallback into a <see cref="CommandRoutine"/>.
    /// </summary>
    /// <param name="routineJsonOrText">The stored routine JSON or plain-text command response.</param>
    /// <returns>The parsed <see cref="CommandRoutine"/>.</returns>
    public static CommandRoutine Parse(string? routineJsonOrText)
    {
        if (string.IsNullOrWhiteSpace(routineJsonOrText))
        {
            return Empty;
        }

        string trimmed = routineJsonOrText.Trim();
        if (!trimmed.StartsWith('{'))
        {
            return FromPlainText(trimmed);
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(trimmed);
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !TryGetPropertyIgnoreCase(doc.RootElement, "steps", out JsonElement stepsElement)
                || stepsElement.ValueKind != JsonValueKind.Array)
            {
                return FromPlainText(trimmed);
            }

            List<CommandRoutineStep> steps = [];
            foreach (JsonElement stepElement in stepsElement.EnumerateArray())
            {
                if (stepElement.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (TryGetPropertyIgnoreCase(stepElement, "$type", out JsonElement typeElement)
                    && typeElement.ValueKind == JsonValueKind.String)
                {
                    string? stepType = typeElement.GetString();
                    if (string.Equals(stepType, ChatSendActionStep.StepType, StringComparison.OrdinalIgnoreCase))
                    {
                        string message = TryGetPropertyIgnoreCase(stepElement, "message", out JsonElement messageElement)
                            && messageElement.ValueKind == JsonValueKind.String
                            ? messageElement.GetString() ?? string.Empty
                            : string.Empty;

                        steps.Add(new ChatSendActionStep(message));
                        continue;
                    }

                    steps.Add(new UnknownCommandRoutineStep(stepType ?? string.Empty, stepElement.GetRawText()));
                    continue;
                }

                CommandRoutineStep? deserialized = stepElement.Deserialize<CommandRoutineStep>(_jsonOptions);
                if (deserialized is not null)
                {
                    steps.Add(deserialized);
                }
            }

            return new CommandRoutine(steps.AsReadOnly());
        }
        catch (JsonException)
        {
            return FromPlainText(trimmed);
        }
    }

    /// <summary>
    /// Serializes this routine into Tier 1 JSON (<c>{"steps":[...]}</c>).
    /// </summary>
    /// <returns>The serialized JSON string.</returns>
    public string ToJson() => JsonSerializer.Serialize(this, _jsonOptions);

    /// <summary>
    /// Returns all chat message templates produced by <see cref="ChatSendActionStep"/> steps in this routine.
    /// </summary>
    /// <returns>A read-only list of message strings.</returns>
    public IReadOnlyList<string> GetChatMessages() =>
        [.. this.Steps.OfType<ChatSendActionStep>().Select(static step => step.Message)];

    /// <summary>
    /// Attempts to find a JSON object property using case-insensitive comparison.
    /// </summary>
    /// <param name="element">The JSON object element to search.</param>
    /// <param name="propertyName">The target property name.</param>
    /// <param name="value">When this method returns <see langword="true"/>, contains the matching property value.</param>
    /// <returns><see langword="true"/> if a matching property was found; otherwise, <see langword="false"/>.</returns>
    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
