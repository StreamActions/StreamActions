using System.Text.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Common.Json.Serialization;

/// <summary>
/// Converts an enum to/from a JSON string in lower case.
/// </summary>
public sealed class JsonLowerCaseEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    #region Public Methods

    /// <inheritdoc/>
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string val = reader.GetString()!;

        foreach (string name in Enum.GetNames<T>())
        {
            if (name.Equals(val, StringComparison.InvariantCultureIgnoreCase))
            {
                return Enum.Parse<T>(name);
            }
        }

        return default;
    }

    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Intentional")]
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) => writer?.WriteStringValue(Enum.GetName(value)?.ToLowerInvariant());

    #endregion Public Methods
}
