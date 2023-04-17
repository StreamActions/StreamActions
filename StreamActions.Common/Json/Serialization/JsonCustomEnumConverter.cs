using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StreamActions.Common.Json.Serialization;

/// <summary>
/// Converts an enum to/from a JSON string as defined by an associated <see cref="JsonCustomEnumAttribute"/>.
/// </summary>
public sealed class JsonCustomEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    #region Public Methods

    /// <inheritdoc/>
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string val = reader.GetString()!;

        foreach (string eval in Enum.GetNames<T>())
        {
            MemberInfo? memberInfo = typeof(T).GetMember(eval).FirstOrDefault();
            if (memberInfo is not null)
            {
                Attribute? attribute = Attribute.GetCustomAttribute(memberInfo, typeof(JsonCustomEnumAttribute));
                if (attribute is not null && ((attribute as JsonCustomEnumAttribute)?.Value.Equals(val, StringComparison.InvariantCultureIgnoreCase) ?? false))
                {
                    return Enum.Parse<T>(eval);
                }
            }
        }

        return default;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        MemberInfo? memberInfo = typeof(T).GetMember(Enum.GetName(typeof(T), value) ?? "__").FirstOrDefault();
        if (memberInfo is not null)
        {
            Attribute? attribute = Attribute.GetCustomAttribute(memberInfo, typeof(JsonCustomEnumAttribute));
            if (attribute is not null)
            {
                writer?.WriteStringValue((attribute as JsonCustomEnumAttribute)?.Value);
            }
        }

        writer?.WriteStringValue(Enum.GetName(value));
    }

    #endregion Public Methods
}
