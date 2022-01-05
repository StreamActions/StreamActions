/*
 * Copyright © 2019-2022 StreamActions Team
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *  http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
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
