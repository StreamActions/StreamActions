/*
 * This file is part of StreamActions.
 * Copyright © 2019-2025 StreamActions Team (streamactions.github.io)
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

using StreamActions.JsonDocuments;
using System;
using System.Globalization;

namespace StreamActions.EventArgs
{
    /// <summary>
    /// Indicated that the <see cref="Plugins.I18n.CurrentCulture"/> or <see cref="Plugins.I18n.GlobalCulture"/> has changed,
    /// and provides a copy of the old culture.
    /// </summary>
    public class OnCultureChangedArgs
    {
        #region Public Properties

        /// <summary>
        /// The channel whose culture was changed. <c>*</c> for the <see cref="Plugins.I18n.GlobalCulture"/>.
        /// </summary>
        public string Channel { get; set; }

        /// <summary>
        /// The old culture that is no longer being used.
        /// </summary>
        public CultureInfo OldCulture { get; set; }

        /// <summary>
        /// A WeakReference to the old <see cref="I18nDocument"/> that is no longer being used.
        /// </summary>
        public WeakReference<I18nDocument> OldI18nDocument { get; set; }

        #endregion Public Properties
    }
}
