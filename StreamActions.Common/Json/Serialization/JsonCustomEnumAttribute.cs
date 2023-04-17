namespace StreamActions.Common.Json.Serialization;

/// <summary>
/// Provides the JSON string value for the attached enum value, for serialization by <see cref="JsonCustomEnumConverter{T}"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class JsonCustomEnumAttribute : Attribute
{
    #region Public Constructors

    /// <summary>
    /// Attribute Constructor.
    /// </summary>
    /// <param name="value">The serialized value of the enum value.</param>
    public JsonCustomEnumAttribute(string value) => this.Value = value;

    #endregion Public Constructors

    #region Public Properties

    /// <summary>
    /// The serialized value of the enum value.
    /// </summary>
    public string Value { get; init; }

    #endregion Public Properties
}
