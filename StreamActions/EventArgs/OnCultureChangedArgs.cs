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
