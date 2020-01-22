using MongoDB.Bson.Serialization.Attributes;
using StreamActions.GraphQL.Connections;

namespace StreamActions.Database.Documents
{
    /// <summary>
    /// Represents a permission assigned to a <see cref="PermissionGroupDocument"/>.
    /// </summary>
    public class PermissionDocument : ICursorable
    {
        #region Public Properties

        /// <summary>
        /// If <c>true</c>, permission is explicitly denied; otherwise, permission is allowed.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        [BsonDefaultValue(false)]
        public bool IsDenied { get; set; }

        /// <summary>
        /// The unique permission name that is being represented.
        /// </summary>
        [BsonElement]
        [BsonRequired]
        [BsonId]
        public string PermissionName { get; set; }

        #endregion Public Properties

        #region Public Methods

        public string GetCursor() => this.PermissionName;

        #endregion Public Methods
    }
}