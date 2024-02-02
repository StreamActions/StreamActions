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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace StreamActions.JsonDocuments
{
    /// <summary>
    /// A document that contains i18n data.
    /// </summary>
    public class I18nDocument
    {
        #region Public Properties

        /// <summary>
        /// An empty I18nDocument for usage as a default. Must be kept empty.
        /// </summary>
        [JsonIgnore]
        public static I18nDocument Empty => _empty.Value;

        /// <summary>
        /// A Dictionary containing Dictionaries of i18n pairs. Key is category; value is Dictionary. Inner Dictionary key is string key; Inner Dictionary value is i18ned string.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "JsonDocument")]
        public Dictionary<string, Dictionary<string, string>> I18nData { get; set; }

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Merges another <see cref="I18nDocument"/> into the current one.
        /// </summary>
        /// <param name="from">The <see cref="I18nDocument"/> to merge.</param>
        public void Merge(I18nDocument from)
        {
            if (from is null)
            {
                throw new ArgumentNullException(nameof(from));
            }

            from.I18nData.ToList().ForEach(x => this.I18nData[x.Key] = x.Value);
        }

        #endregion Public Methods

        #region Private Fields

        /// <summary>
        /// Field that backs <see cref="Empty"/>.
        /// </summary>
        private static readonly Lazy<I18nDocument> _empty = new Lazy<I18nDocument>(() => new I18nDocument());

        #endregion Private Fields
    }
}
