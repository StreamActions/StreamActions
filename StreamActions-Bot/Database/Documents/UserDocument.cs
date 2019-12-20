namespace StreamActions.Database.Documents
{
    /// <summary>
    /// A document representing a Twitch user.
    /// </summary>
    public class UserDocument
    {
        #region Public Properties

        /// <summary>
        /// The users <see cref="BotGlobalFlag"/>.
        /// </summary>
        public BotGlobalFlag BotFlag { get; set; }

        /// <summary>
        /// The users <see cref="TwitchBroadcasterFlag"/>.
        /// </summary>
        public TwitchBroadcasterFlag BroadcasterFlag { get; set; }

        /// <summary>
        /// The users Display Name on Twitch, if set.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// The users Email address, if available.
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// The users Twitch user Id.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The users login name on Twitch
        /// </summary>
        public string Login { get; set; }

        /// <summary>
        /// The users <see cref="TwitchStaffFlag"/>.
        /// </summary>
        public TwitchStaffFlag StaffFlag { get; set; }

        #endregion Public Properties

        #region Public Enums

        /// <summary>
        /// A flag representing global permissions on the bot.
        /// </summary>
        public enum BotGlobalFlag
        {
            /// <summary>
            /// No additional permissions.
            /// </summary>
            None,

            /// <summary>
            /// SuperAdmin; all permissions granted.
            /// </summary>
            SuperAdmin,

            /// <summary>
            /// Banned; all permissions denied.
            /// </summary>
            Banned
        }

        /// <summary>
        /// A flag representing a channels Twitch partner status.
        /// </summary>
        public enum TwitchBroadcasterFlag
        {
            /// <summary>
            /// Normal channel.
            /// </summary>
            None,

            /// <summary>
            /// Affiliate status.
            /// </summary>
            Affiliate,

            /// <summary>
            /// Partner status.
            /// </summary>
            Partner
        }

        /// <summary>
        /// A flag representing Twitch staff.
        /// </summary>
        public enum TwitchStaffFlag
        {
            /// <summary>
            /// Normal viewer.
            /// </summary>
            None,

            /// <summary>
            /// Twitch staff.
            /// </summary>
            Staff,

            /// <summary>
            /// Twitch admin.
            /// </summary>
            Admin,

            /// <summary>
            /// Twitch global moderator.
            /// </summary>
            GlobalMod
        }

        #endregion Public Enums
    }
}