/*
 * This file is part of StreamActions.
 * Copyright © 2019-2022 StreamActions Team (streamactions.github.io)
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

using StreamActions.MemoryDocuments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace StreamActions
{
    internal class TwitchMessageCache
    {
        #region Public Methods

        /// <summary>
        /// Method that will consume each Twitch message and add it to our cache list.
        /// </summary>
        /// <param name="message">Message from Twitch <see cref="ChatMessage"/>.</param>
        public void Consume(ChatMessage message)
        {
            lock (this._lock)
            {
                this._messages.Add(new ChatMessageDocument
                {
                    Message = message
                });
            }
        }

        /// <summary>
        /// Gets the number of messages a user sent in a time period.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="channelId">The ID of the channel which to choose the message from.</param>
        /// <param name="period">Time period of which the messages have been sent.</param>
        /// <returns>Number of messages sent in that period.</returns>
        public int GetNumberOfMessageSentFromUserInPeriod(string userId, string channelId, DateTime period)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentNullException(nameof(userId));
            }

            if (string.IsNullOrEmpty(channelId))
            {
                throw new ArgumentNullException(nameof(channelId));
            }

            lock (this._lock)
            {
                return this._messages.Where(m => m.Message.RoomId.Equals(channelId, StringComparison.OrdinalIgnoreCase)
                    && m.Message.UserId.Equals(userId, StringComparison.OrdinalIgnoreCase)
                    && m.SentAt <= period).Count();
            }
        }

        /// <summary>
        /// Method that will return a list of users that sent a messages maching the matchMessage variable.
        /// </summary>
        /// <param name="matchMessage">Message that should be matched.</param>
        /// <param name="channelId">The ID of the channel which to choose the message from.</param>
        /// <returns>List of users, can return no users if none are found.</returns>
        public List<string> GetUsersWhoSentMatchingMessage(Regex matchMessage, string channelId)
        {
            if (matchMessage is null)
            {
                throw new ArgumentNullException(nameof(matchMessage));
            }

            if (string.IsNullOrEmpty(channelId))
            {
                throw new ArgumentNullException(nameof(channelId));
            }

            lock (this._lock)
            {
                return this._messages.Where(m => m.Message.RoomId.Equals(channelId, StringComparison.Ordinal)
                     && matchMessage.IsMatch(m.Message.Message))
                    .GroupBy(m => m.Message.Username)
                    .Select(m => m.First().Message.Username)
                    .ToList();
            }
        }

        #endregion Public Methods

        #region Internal Properties

        /// <summary>
        /// Singleton of <see cref="TwitchMessageCache"/>.
        /// </summary>
        internal static TwitchMessageCache Instance => _instance.Value;

        #endregion Internal Properties

        #region Private Fields

        /// <summary>
        /// Field that backs the <see cref="Instance"/> property.
        /// </summary>
        private static readonly Lazy<TwitchMessageCache> _instance = new Lazy<TwitchMessageCache>(() => new TwitchMessageCache());

        /// <summary>
        /// Used to make the list concurrent.
        /// </summary>
        private readonly object _lock = new object();

        /// <summary>
        /// Message that have been consumed by this cache.
        /// </summary>
        private readonly List<ChatMessageDocument> _messages = new List<ChatMessageDocument>();

        #endregion Private Fields

        #region Private Constructors

        /// <summary>
        /// Class constructor.
        /// </summary>
        private TwitchMessageCache() => this.ScheduleMessageCacheRemover();

        #endregion Private Constructors

        #region Private Methods

        /// <summary>
        /// Method that will create the job to remove messages that are older than 5 minutes from the cache.
        /// </summary>
        private void ScheduleMessageCacheRemover() => Scheduler.AddJob(() =>
          {
              DateTime pastTime = DateTime.Now.AddMinutes(-5);

              lock (this._lock)
              {
                  this._messages.ForEach((m =>
                  {
                      if (m.SentAt <= pastTime)
                      {
                          _ = this._messages.Remove(m);
                      }
                  }));
              }
          }, s => s.WithName("MessageCacheRemover").ToRunEvery(5).Minutes());

        #endregion Private Methods
    }
}
