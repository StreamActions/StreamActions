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

using System.Text.Json.Serialization;
using System.Text.Json;
using System.Reflection;
using System.Collections.Immutable;
using StreamActions.Common.Logger;

namespace StreamActions.Common.Json.Serialization;

/// <summary>
/// Implements a JSON converter which allows custom serialization or deserialization to be implemented based on property name.
/// </summary>
/// <remarks>
/// This <see cref="JsonConverter{T}"/> only supports serializing and deserializing properties and fields into a JSON object.
/// </remarks>
/// <typeparam name="T">A class or record which is being serialized and deserialized.</typeparam>
public abstract class JsonCustomConverter<T> : JsonConverter<T>
{
    #region Public Methods

    /// <summary>
    /// Reads and converts the JSON to type <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>
    /// If <see cref="ShouldUseDefaultConversion(string, string)"/> returns <see langword="false"/>
    /// for a given field or property, control is passed to <see cref="Read(ref Utf8JsonReader, Utf8JsonReader, Type, JsonSerializerOptions, T, string, string)"/>;
    /// otherwise, <see cref="JsonSerializer.Deserialize(JsonElement, Type, JsonSerializerOptions?)"/> is used.
    /// </remarks>
    /// <param name="reader">The reader.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    /// <returns>The deserialized <typeparamref name="T"/>.</returns>
    public override sealed T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        T obj = this.CreateInstance();

        Utf8JsonReader newReader = reader;
        ImmutableDictionary<string, string> propertyMap = this.MapFromJsonPropertyNames();
        string propertyName = "";
        string jsonPropertyName = "";
        bool shouldUseDefault = true;

        while (reader.Read())
        {
            if (reader.TokenType is JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType is JsonTokenType.PropertyName || shouldUseDefault)
            {
                if (reader.TokenType is not JsonTokenType.PropertyName)
                {
                    this.DefaultRead(ref reader, obj, propertyName, options);
                }
                else
                {
                    jsonPropertyName = reader.GetString()!;
                    propertyName = propertyMap.GetValueOrDefault(jsonPropertyName, jsonPropertyName);
                    shouldUseDefault = this.ShouldUseDefaultConversion(propertyName, jsonPropertyName);
                }
            }
            else
            {
                this.Read(ref reader, newReader, typeToConvert, options, obj, propertyName, jsonPropertyName);
            }
        }

        return obj;
    }

    /// <summary>
    /// Reads and converts an object of type <typeparamref name="T"/> to JSON.
    /// </summary>
    /// <remarks>
    /// If <see cref="ShouldUseDefaultConversion(string, string)"/> returns
    /// <see langword="false"/> for a given field or property, control is passed to <see cref="Write(Utf8JsonWriter, T, JsonSerializerOptions, string, string, object?)"/>;
    /// otherwise, <see cref="JsonSerializer.Serialize(Utf8JsonWriter, object?, Type, JsonSerializerOptions?)"/> is used.
    /// </remarks>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The value to convert to JSON.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    public override sealed void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        if (writer is null)
        {
            return;
        }

        ImmutableDictionary<string, string> propertyMap = this.MapToJsonPropertyNames();
        MemberInfo[] memberInfo = this.GetTypeofT().GetMembers();

        foreach (MemberInfo member in memberInfo)
        {
            if (member.MemberType is MemberTypes.Property or MemberTypes.Field)
            {
                string propertyName = member.Name;
                string jsonPropertyName = propertyMap.GetValueOrDefault(propertyName, propertyName);
                object? memberValue = this.GetMember(value, propertyName);

                if (this.ShouldSerializeProperty(propertyName, jsonPropertyName, memberValue))
                {
                    if (this.ShouldUseDefaultConversion(propertyName, jsonPropertyName))
                    {
                        this.DefaultWrite(writer, jsonPropertyName, memberValue, options);
                    }
                    else
                    {
                        this.Write(writer, value, options, propertyName, jsonPropertyName, memberValue);
                    }
                }
            }
        }
    }

    #endregion Public Methods

    #region Protected Methods

    /// <summary>
    /// Creates a new instance of <typeparamref name="T"/>.
    /// </summary>
    /// <returns>A new instance of <typeparamref name="T"/>.</returns>
    /// <exception cref="ArgumentNullException"><see cref="Activator.CreateInstance(Type)"/> returned <see langword="null"/>.</exception>
    protected virtual T CreateInstance()
    {
        T? obj = (T?)Activator.CreateInstance(typeof(T));

        return obj is null ? throw new ArgumentNullException(nameof(obj)) : obj;
    }

    /// <summary>
    /// Performs the default JSON to CLR-type conversion for the value.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="obj">The object instance that data is being deserialized into.</param>
    /// <param name="propertyName">The property name to be deserialized.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1045:Do not pass types by reference", Justification = "Passing struct with state.")]
    protected void DefaultRead(ref Utf8JsonReader reader, T obj, string propertyName, JsonSerializerOptions options)
    {
        Type? t = this.GetMemberType(obj, propertyName);
        if (t is not null)
        {
            this.SetMember(obj, propertyName, JsonSerializer.Deserialize(ref reader, t, options));
        }
    }

    /// <summary>
    /// Performs the default CLR-type to JSON conversion for the value.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="jsonPropertyName">The JSON property name to serialize the value into.</param>
    /// <param name="memberValue">The value to serialize.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    protected void DefaultWrite(Utf8JsonWriter writer, string jsonPropertyName, object? memberValue, JsonSerializerOptions options)
    {
        if (writer is not null)
        {
            writer.WritePropertyName(jsonPropertyName);
            JsonSerializer.Serialize(writer, memberValue, options);
        }
    }

    /// <summary>
    /// Uses reflection to get the value of a field from <paramref name="obj"/>.
    /// </summary>
    /// <param name="obj">The object to retrieve the value from.</param>
    /// <param name="fieldName">The field to retrieve.</param>
    /// <returns>The value.</returns>
    protected object? GetField(T obj, string fieldName) => obj?.GetType().GetField(fieldName)?.GetValue(obj);

    /// <summary>
    /// Uses reflection to get the type of a field from <paramref name="obj"/>.
    /// </summary>
    /// <param name="obj">The object to retrieve the type from.</param>
    /// <param name="fieldName">The field name to retrieve.</param>
    /// <returns>The type of the field.</returns>
    protected Type? GetFieldType(T obj, string fieldName) => obj?.GetType().GetField(fieldName)?.FieldType;

    /// <summary>
    /// Uses reflection to get the value of a field or property from <paramref name="obj"/>.
    /// </summary>
    /// <param name="obj">The object to retrieve the value from.</param>
    /// <param name="memberName">The member to retrieve.</param>
    /// <returns>The value.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0072:Add missing cases", Justification = "Intentional.")]
    protected object? GetMember(T obj, string memberName) => (obj?.GetType().GetMember(memberName)?.FirstOrDefault()?.MemberType) switch
    {
        MemberTypes.Property => this.GetProperty(obj, memberName),
        MemberTypes.Field => this.GetField(obj, memberName),
        _ => null,
    };

    /// <summary>
    /// Uses reflection to get the type of a field or property from <paramref name="obj"/>.
    /// </summary>
    /// <param name="obj">The object to retrieve the type from.</param>
    /// <param name="memberName">The member name to retrieve.</param>
    /// <returns>The type of the member.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0072:Add missing cases", Justification = "Intentional.")]
    protected Type? GetMemberType(T obj, string memberName) => (obj?.GetType().GetMember(memberName)?.FirstOrDefault()?.MemberType) switch
    {
        MemberTypes.Property => this.GetPropertyType(obj, memberName),
        MemberTypes.Field => this.GetFieldType(obj, memberName),
        _ => null,
    };

    /// <summary>
    /// Uses reflection to get the value of a property from <paramref name="obj"/>.
    /// </summary>
    /// <param name="obj">The object to retrieve the value from.</param>
    /// <param name="propertyName">The property to retrieve.</param>
    /// <returns>The value.</returns>
    protected object? GetProperty(T obj, string propertyName) => obj?.GetType().GetProperty(propertyName)?.GetValue(obj);

    /// <summary>
    /// Uses reflection to get the type of a property from <paramref name="obj"/>.
    /// </summary>
    /// <param name="obj">The object to retrieve the type from.</param>
    /// <param name="propertyName">The property name to retrieve.</param>
    /// <returns>The type of the property.</returns>
    protected Type? GetPropertyType(T obj, string propertyName) => obj?.GetType().GetProperty(propertyName)?.PropertyType;

    /// <summary>
    /// Returns the <c>typeof</c> <typeparamref name="T"/> that is created by <see cref="CreateInstance"/>.
    /// </summary>
    /// <returns>The <c>typeof</c> <typeparamref name="T"/>.</returns>
    protected virtual Type GetTypeofT() => typeof(T);

    /// <summary>
    /// Maps <see cref="JsonPropertyNameAttribute"/> to the members of <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>
    /// Only members which have <see cref="JsonPropertyNameAttribute"/> are included in the returned dictionary.
    /// </remarks>
    /// <returns>An <see cref="ImmutableDictionary{TKey, TValue}"/> with keys of <see cref="JsonPropertyNameAttribute.Name"/> and values of member names.</returns>
    protected virtual ImmutableDictionary<string, string> MapFromJsonPropertyNames()
    {
        Dictionary<string, string> map = [];
        MemberInfo[] memberInfo = this.GetTypeofT().GetMembers();

        foreach (MemberInfo member in memberInfo)
        {
            Attribute? attribute = Attribute.GetCustomAttribute(member, typeof(JsonPropertyNameAttribute));
            if (attribute is not null)
            {
                map.Add((attribute as JsonPropertyNameAttribute)!.Name, member.Name);
            }
        }

        return map.ToImmutableDictionary();
    }

    /// <summary>
    /// Maps the memebrs of <typeparamref name="T"/> to <see cref="JsonPropertyNameAttribute"/>.
    /// </summary>
    /// <remarks>
    /// Only members which have <see cref="JsonPropertyNameAttribute"/> are included in the returned dictionary.
    /// </remarks>
    /// <returns>An <see cref="ImmutableDictionary{TKey, TValue}"/> with keys of member names and values of <see cref="JsonPropertyNameAttribute.Name"/>.</returns>
    protected virtual ImmutableDictionary<string, string> MapToJsonPropertyNames()
    {
        Dictionary<string, string> map = [];
        MemberInfo[] memberInfo = this.GetTypeofT().GetMembers();

        foreach (MemberInfo member in memberInfo)
        {
            Attribute? attribute = Attribute.GetCustomAttribute(member, typeof(JsonPropertyNameAttribute));
            if (attribute is not null)
            {
                map.Add(member.Name, (attribute as JsonPropertyNameAttribute)!.Name);
            }
        }

        return map.ToImmutableDictionary();
    }

    /// <summary>
    /// Custom handler for deserializing part of the JSON data for <typeparamref name="T"/> when <see langword="false"/> is returned
    /// by <see cref="ShouldUseDefaultConversion(string, string)"/>.
    /// </summary>
    /// <remarks>
    /// The <paramref name="newReader"/> should only be used for finding and reading other properties which are required to
    /// determine how <paramref name="propertyName"/> is deserialized. The actual reading of <paramref name="propertyName"/> must be done using <paramref name="reader"/>.
    /// <br/><br/>
    /// If <see cref="Utf8JsonReader.TokenType"/> is <see cref="JsonTokenType.StartObject"/> or <see cref="JsonTokenType.StartArray"/> when this method is entered,
    /// the implementation must read the appropriate <see cref="JsonTokenType.EndObject"/> or <see cref="JsonTokenType.EndArray"/> token before returning.
    /// </remarks>
    /// <param name="reader">The reader.</param>
    /// <param name="newReader">A copy of <paramref name="reader"/> at the initial state before <see cref="Read(ref Utf8JsonReader, Type, JsonSerializerOptions)"/> started deserializing.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    /// <param name="obj">The object instance that data is being deserialized into.</param>
    /// <param name="propertyName">The property name to be deserialized.</param>
    /// <param name="propertyName">The <see cref="JsonPropertyNameAttribute.Name"/> for the property; <paramref name="propertyName"/> if not specified.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1045:Do not pass types by reference", Justification = "Passing struct with state.")]
    protected abstract void Read(ref Utf8JsonReader reader, Utf8JsonReader newReader, Type typeToConvert, JsonSerializerOptions options, T obj, string propertyName, string jsonPropertyName);

    /// <summary>
    /// Uses reflection to set the value of a field on <paramref name="obj"/>.
    /// </summary>
    /// <param name="obj">The object to modify.</param>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="value">The value to set.</param>
    protected void SetField(T obj, string fieldName, object? value) => obj?.GetType().GetField(fieldName)?.SetValue(obj, value);

    /// <summary>
    /// Uses reflection to set the value of a field or property on <paramref name="obj"/>.
    /// </summary>
    /// <param name="obj">The object to modify.</param>
    /// <param name="memberName">The name of the member.</param>
    /// <param name="value">The value to set.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0010:Add missing cases", Justification = "Intentional.")]
    protected void SetMember(T obj, string memberName, object? value)
    {
        switch (obj?.GetType().GetMember(memberName)?.FirstOrDefault()?.MemberType)
        {
            case MemberTypes.Property:
                this.SetProperty(obj, memberName, value);
                break;

            case MemberTypes.Field:
                this.SetField(obj, memberName, value);
                break;
        }
    }

    /// <summary>
    /// Uses reflection to set the value of a property on <paramref name="obj"/>.
    /// </summary>
    /// <param name="obj">The object to modify.</param>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="value">The value to set.</param>
    protected void SetProperty(T obj, string propertyName, object? value) => obj?.GetType().GetProperty(propertyName)?.SetValue(obj, value);

    /// <summary>
    /// Indicates if the given property of <typeparamref name="T"/> should be serialized.
    /// </summary>
    /// <remarks>
    /// The default implementation returns <see langword="true"/> if there is no <see cref="JsonIgnoreAttribute"/> or the
    /// <see cref="JsonIgnoreAttribute.Condition"/> indicates that the property should not be ignored.
    /// <br/><br/>
    /// The default implementation always returns <see langword="false"/> if the <see cref="MemberInfo"/> can not be retrieved.
    /// </remarks>
    /// <param name="propertyName">The property to potentially serialize.</param>
    /// <param name="jsonPropertyName">The <see cref="JsonPropertyNameAttribute.Name"/> for the property; <paramref name="propertyName"/> if not specified.</param>
    /// <param name="value">The value to serialize.</param>
    /// <returns><see langword="true"/> if the property should be serialized.</returns>
    protected virtual bool ShouldSerializeProperty(string propertyName, string jsonPropertyName, object? value)
    {
        MemberInfo? memberInfo = this.GetTypeofT().GetMember(propertyName).FirstOrDefault();
        if (memberInfo is not null)
        {
            Attribute? attribute = Attribute.GetCustomAttribute(memberInfo, typeof(JsonIgnoreAttribute));
            if (attribute is not null)
            {
                JsonIgnoreCondition? condition = (attribute as JsonIgnoreAttribute)?.Condition;

                if (condition is not null)
                {
                    if (condition is JsonIgnoreCondition.Never)
                    {
                        return true;
                    }
                    else if (condition is JsonIgnoreCondition.Always)
                    {
                        return false;
                    }
                    else
                    {
                        object? defaultValue = Util.GetDefault(value);
                        if (defaultValue is null && value is null)
                        {
                            return condition is not JsonIgnoreCondition.WhenWritingDefault and not JsonIgnoreCondition.WhenWritingNull;
                        }
                        else if (value == defaultValue)
                        {
                            return condition is not JsonIgnoreCondition.WhenWritingDefault;
                        }
                        else if (value is null)
                        {
                            return condition is not JsonIgnoreCondition.WhenWritingNull;
                        }
                    }
                }
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Indicates if the given property should use the default conversion provided by <see cref="JsonSerializer"/>.
    /// </summary>
    /// <remarks>
    /// When <see langword="false"/> is returned for a given property in <see cref="Read(ref Utf8JsonReader, Type, JsonSerializerOptions)"/>,
    /// control will be passed to <see cref="Read(ref Utf8JsonReader, ref Utf8JsonReader, Type, JsonSerializerOptions, T, string, string)"/>, which should
    /// perform the necessary calls to read the value from the reader and then set the value onto the object provided in the first parameter.
    /// <br/><br/>
    /// When <see langword="false"/> is returned for a given property in <see cref="Write(Utf8JsonWriter, T, JsonSerializerOptions)"/>,
    /// control will be passed to <see cref="Write(Utf8JsonWriter, T, JsonSerializerOptions, string, string, object)"/>, which should
    /// perform the necessary calls to write the property name and value onto the <see cref="Utf8JsonWriter"/>.
    /// </remarks>
    /// <param name="propertyName">The property name being converted.</param>
    /// <param name="jsonPropertyName">The <see cref="JsonPropertyNameAttribute.Name"/> for the property; <paramref name="propertyName"/> if not specified.</param>
    /// <returns><see langword="true"/> if the default conversion should be used.</returns>
    protected virtual bool ShouldUseDefaultConversion(string propertyName, string jsonPropertyName) => true;

    /// <summary>
    /// Custom handler for serializing part of a <typeparamref name="T"/> to JSON when <see langword="false"/> is returned
    /// by <see cref="ShouldUseDefaultConversion(string, string)"/>.
    /// </summary>
    /// <remarks>
    /// Implementations must write the <paramref name="jsonPropertyName"/> to the <paramref name="writer"/> first if choosing to serialize the <paramref name="value"/>.
    /// </remarks>
    /// <param name="writer">The writer.</param>
    /// <param name="obj">The object being serialized.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    /// <param name="propertyName">The CLR property name being serialized.</param>
    /// <param name="jsonPropertyName">The <see cref="JsonPropertyNameAttribute.Name"/> for the property; <paramref name="propertyName"/> if not specified.</param>
    /// <param name="value">The value being serialized.</param>
    protected abstract void Write(Utf8JsonWriter writer, T obj, JsonSerializerOptions options, string propertyName, string jsonPropertyName, object? value);

    #endregion Protected Methods
}
