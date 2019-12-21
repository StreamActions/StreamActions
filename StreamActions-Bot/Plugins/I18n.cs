/*
 * Copyright © 2019-2020 StreamActions Team
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

using StreamActions.EventArgs;
using StreamActions.JsonDocuments;
using StreamActions.Plugin;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace StreamActions.Plugins
{
    public class I18n : IPlugin
    {
        #region Public Events

        public event EventHandler<OnCultureChangedArgs> OnCultureChanged;

        #endregion Public Events

        #region Public Properties

        /// <summary>
        /// Retrieves the current instance of <see cref="I18n"/>.
        /// </summary>
        public static I18n Instance
        {
            get
            {
                if (PluginManager.Instance.Plugins.TryGetValue(typeof(I18n).FullName, out WeakReference<IPlugin> instance))
                {
                    if (instance.TryGetTarget(out IPlugin target))
                    {
                        return typeof(I18n).IsAssignableFrom(target.GetType()) ? (I18n)target : null;
                    }
                }

                return null;
            }
        }

        public bool AlwaysEnabled => true;
        public CultureInfo CurrentCulture => this._currentCulture;
        public string PluginAuthor => "StreamActions Team";

        public string PluginDescription => "Provides i18n support";

        public string PluginName => "I18n";

        public Uri PluginUri => throw new NotImplementedException();

        public string PluginVersion => "1.0.0";

        #endregion Public Properties

        #region Public Methods

        public void Disabled()
        {
        }

        public void Enabled()
        {
            //TODO: Load the current language from the settings file.
        }

        /// <summary>
        /// Attempts to retrieve the specified i18n replacement string.
        /// </summary>
        /// <param name="category">The category to select from.</param>
        /// <param name="key">The string key to select.</param>
        /// <param name="defVal">The default value to return if either the <paramref name="category"/> or <paramref name="key"/> do not exist; <c>null</c> if not provided</param>
        /// <param name="document">The <see cref="I18nDocument"/> to use; <c>null</c> for the document of the <see cref="CurrentCulture"/>.</param>
        /// <returns>The i18n replacement string, if present; <paramref name="defVal"/> otherwise.</returns>
        public string Get(string category, string key, string defVal = null, I18nDocument document = null)
        {
            if (document == null)
            {
                document = this._i18nDocument;
            }

            if (document.I18nData.TryGetValue(category, out Dictionary<string, string> data))
            {
                if (data.TryGetValue(key, out string value))
                {
                    return value;
                }
            }

            return defVal;
        }

        #endregion Public Methods

        #region Private Fields

        /// <summary>
        /// Field that backs <see cref="CurrentCulture"/>.
        /// </summary>
        private CultureInfo _currentCulture = CultureInfo.CurrentCulture;

        /// <summary>
        /// An <see cref="I18nDocument"/> containing all currently loaded i18n data.
        /// </summary>
        private I18nDocument _i18nDocument = new I18nDocument();

        #endregion Private Fields

        #region Private Methods

        /// <summary>
        /// Changes the current culture to the specified one.
        /// </summary>
        /// <param name="culture">The <c>languagecode2-country/regioncode2</c> name of the new culture to load.</param>
        /// <exception cref="CultureNotFoundException"><paramref name="culture"/> is not a valid culture name.</exception>
        private async void ChangeCulture(string culture)
        {
            CultureInfo newCulture = new CultureInfo(culture, true);
            I18nDocument oldDocument = this._i18nDocument;

            OnCultureChangedArgs args = new OnCultureChangedArgs()
            {
                OldCulture = this.CurrentCulture,
                OldI18nDocument = new WeakReference<I18nDocument>(oldDocument)
            };

            this._i18nDocument = await this.LoadCulture(newCulture).ConfigureAwait(false);
            this._currentCulture = newCulture;

            OnCultureChanged?.Invoke(this, args);
        }

        /// <summary>
        /// Loads the i18n files for the specified culture and merges it all into a single <see cref="I18nDocument"/>.
        /// The files must be under the <c>i18n/lang-region</c> folder, such as <c>i18n/en-US</c> or <c>i18n/es-ES</c> and have <c>.json</c> extensions.
        /// </summary>
        /// <param name="newCulture">The culture to load.</param>
        /// <returns>An <see cref="I18nDocument"/> containing the merged contents of all valid json files.</returns>
        private async Task<I18nDocument> LoadCulture(CultureInfo newCulture)
        {
            I18nDocument document = new I18nDocument();

            string i18nLocation = Path.GetFullPath(Path.Combine(typeof(Program).Assembly.Location, "i18n", newCulture.Name));

            if (Directory.Exists(i18nLocation))
            {
                foreach (string f in Directory.EnumerateFiles(i18nLocation, "*.json", new EnumerationOptions()
                {
                    RecurseSubdirectories = true
                }))
                {
                    try
                    {
                        using FileStream fs = File.OpenRead(f);
                        document.Merge(await JsonSerializer.DeserializeAsync<I18nDocument>(fs));
                    }
                    catch (JsonException ex) { _ = ex.LineNumber; }
                }
            }

            return document;
        }
    }

    #endregion Private Methods
}