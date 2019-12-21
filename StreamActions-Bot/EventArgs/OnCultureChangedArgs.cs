using StreamActions.JsonDocuments;
using System;
using System.Globalization;

namespace StreamActions.EventArgs
{
    /// <summary>
    /// Indicated that the <see cref="StreamActions.Plugins.I18n.CurrentCulture"/> has changed, and provides a copy of the old culture.
    /// </summary>
    public class OnCultureChangedArgs
    {
        #region Public Properties

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