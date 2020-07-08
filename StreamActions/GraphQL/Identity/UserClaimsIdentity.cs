using StreamActions.Database.Documents.Users;
using System.Security.Claims;

namespace StreamActions.GraphQL.Identity
{
    public class UserClaimsIdentity : ClaimsIdentity
    {
        #region Public Constructors

        public UserClaimsIdentity(UserDocument userDocument) : base() => this._userDocument = userDocument;

        #endregion Public Constructors

        #region Public Properties

        public UserDocument UserDocument => this._userDocument;

        #endregion Public Properties

        #region Private Fields

        private readonly UserDocument _userDocument;

        #endregion Private Fields
    }
}