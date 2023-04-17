using System.Reflection;
using System.Text.Json.Serialization;

namespace StreamActions.Common.Json.Serialization;

/// <summary>
/// Extensions for enums that use attributes in the <see cref="Serialization"/> namespace.
/// </summary>
public static class JsonEnumExtensions
{
    /// <summary>
    /// Converts the value into its serialized JSON value.
    /// </summary>
    /// <typeparam name="T">The enum type to serialize.</typeparam>
    /// <param name="value">The enum value to serialize.</param>
    /// <returns>The converted value, if tagged appropriately with a <see cref="JsonConverterAttribute"/> from the <see cref="Serialization"/> namespace; otherwise, <c>Enum.GetName(value)</c>.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Intentional")]
    public static string? JsonValue<T>(this T value) where T : struct, Enum
    {
        Attribute? attribute = Attribute.GetCustomAttribute(typeof(T), typeof(JsonConverterAttribute));

        if (attribute is not null)
        {
            Type? type = (attribute as JsonConverterAttribute)?.ConverterType;

            if (type is not null)
            {
                if (type is JsonUpperCaseEnumConverter<T>)
                {
                    return Enum.GetName(value)?.ToUpperInvariant();
                }
                else if (type is JsonLowerCaseEnumConverter<T>)
                {
                    return Enum.GetName(value)?.ToLowerInvariant();
                }
                else if (type is JsonCustomEnumConverter<T>)
                {
                    MemberInfo? memberInfo = typeof(T).GetMember(Enum.GetName(value) ?? "__").FirstOrDefault();
                    if (memberInfo is not null)
                    {
                        Attribute? eattribute = Attribute.GetCustomAttribute(memberInfo, typeof(JsonCustomEnumAttribute));
                        if (eattribute is not null)
                        {
                            return (eattribute as JsonCustomEnumAttribute)?.Value;
                        }
                    }
                }
            }
        }

        return Enum.GetName(value);
    }
}
