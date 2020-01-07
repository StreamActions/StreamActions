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
using System.Collections.Concurrent;
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

        /// <summary>
        /// Fires when the current culture has been changed.
        /// </summary>
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

        /// <summary>
        /// Provides the currently active culture of each channel, by id.
        /// </summary>
        public Dictionary<string, WeakReference<CultureInfo>> CurrentCulture
        {
            get
            {
                Dictionary<string, WeakReference<CultureInfo>> value = new Dictionary<string, WeakReference<CultureInfo>>();

                foreach (KeyValuePair<string, CultureInfo> kvp in this._currentCulture)
                {
                    value.Add(kvp.Key, new WeakReference<CultureInfo>(kvp.Value));
                }

                return value;
            }
        }

        public string PluginAuthor => "StreamActions Team";

        public string PluginDescription => "Provides i18n support";

        public string PluginName => "I18n";

        public Uri PluginUri => throw new NotImplementedException();

        public string PluginVersion => "1.0.0";

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Attempts to retrieve the specified i18n replacement string.
        /// </summary>
        /// <param name="category">The category to select from.</param>
        /// <param name="key">The string key to select.</param>
        /// <param name="document">The <see cref="I18nDocument"/> to use.</param>
        /// <param name="defVal">The default value to return if either the <paramref name="category"/> or <paramref name="key"/> do not exist; <c>null</c> if not provided</param>
        /// <returns>The i18n replacement string, if present; <paramref name="defVal"/> otherwise.</returns>
        public static string Get(string category, string key, I18nDocument document, string defVal = null)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
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

        /// <summary>
        /// Loads the i18n files for the specified culture and merges it all into a single <see cref="I18nDocument"/>.
        /// The files must be under the <c>i18n/lang-region</c> folder, such as <c>i18n/en-US</c> or <c>i18n/es-ES</c> and have <c>.json</c> extensions.
        /// </summary>
        /// <param name="newCulture">The culture to load.</param>
        /// <returns>An <see cref="I18nDocument"/> containing the merged contents of all valid json files.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="newCulture"/> is null.</exception>
        public static async Task<I18nDocument> LoadCultureAsync(CultureInfo newCulture)
        {
            if (newCulture == null)
            {
                throw new ArgumentNullException(nameof(newCulture));
            }

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
        /// <param name="culture">The CultureInfo that should be used.</param>
        /// <param name="defVal">The default value to return if either the <paramref name="category"/> or <paramref name="key"/> do not exist; <c>null</c> if not provided</param>
        /// <returns>The i18n replacement string, if present; <paramref name="defVal"/> otherwise.</returns>
        public string Get(string category, string key, CultureInfo culture, string defVal = null)
        {
            if (culture == null)
            {
                throw new ArgumentNullException(nameof(culture));
            }

            return Get(category, key, this._i18nDocuments.GetValueOrDefault<string, I18nDocument>(culture.Name, I18nDocument.Empty), defVal);
        }

        /// <summary>
        /// Attempts to retrieve the specified i18n replacement string.
        /// </summary>
        /// <param name="category">The category to select from.</param>
        /// <param name="key">The string key to select.</param>
        /// <param name="channel">The channel id whose CurrentCulture should be used.</param>
        /// <param name="defVal">The default value to return if either the <paramref name="category"/> or <paramref name="key"/> do not exist; <c>null</c> if not provided</param>
        /// <returns>The i18n replacement string, if present; <paramref name="defVal"/> otherwise.</returns>
        public string Get(string category, string key, string channel, string defVal = null)
        {
            if (channel == null)
            {
                throw new ArgumentNullException(nameof(channel));
            }

            return Get(category, key, this._i18nDocuments.GetValueOrDefault<string, I18nDocument>(this._currentCulture.GetValueOrDefault(channel, new CultureInfo("en-US", false)).Name, I18nDocument.Empty), defVal);
        }

        #endregion Public Methods

        #region Private Fields

        /// <summary>
        /// Field that backs the <see cref="CurrentCulture"/> property.
        /// </summary>
        private readonly ConcurrentDictionary<string, CultureInfo> _currentCulture = new ConcurrentDictionary<string, CultureInfo>();

        /// <summary>
        /// An <see cref="I18nDocument"/> containing all currently loaded i18n data.
        /// </summary>
        private readonly ConcurrentDictionary<string, I18nDocument> _i18nDocuments = new ConcurrentDictionary<string, I18nDocument>();

        #endregion Private Fields

        #region Private Methods

        /// <summary>
        /// Changes the current culture to the specified one.
        /// </summary>
        /// <param name="channel">The channel id whose culture should be changed.</param>
        /// <param name="culture">The <c>languagecode2-country/regioncode2</c> name of the new culture to load.</param>
        /// <param name="useUseroverride">A Boolean that denotes whether to use the user-selected culture settings (<c>true</c>) or the default culture settings (<c>false</c>).</param>
        /// <exception cref="CultureNotFoundException"><paramref name="culture"/> is not a valid culture name.</exception>
        private async void ChangeCulture(string channel, string culture, bool useUseroverride = false)
        {
            CultureInfo newCulture = new CultureInfo(culture, useUseroverride);
            I18nDocument oldDocument = this._i18nDocuments.GetValueOrDefault<string, I18nDocument>(this._currentCulture.GetValueOrDefault(channel, new CultureInfo("en-US", useUseroverride)).Name, I18nDocument.Empty);

            OnCultureChangedArgs args = new OnCultureChangedArgs
            {
                Channel = channel,
                OldCulture = this._currentCulture.GetValueOrDefault(channel, new CultureInfo("en-US", useUseroverride)),
                OldI18nDocument = new WeakReference<I18nDocument>(oldDocument)
            };

            if (!this._i18nDocuments.ContainsKey(newCulture.Name))
            {
                _ = this._i18nDocuments.TryAdd(newCulture.Name, await LoadCultureAsync(newCulture).ConfigureAwait(false));
            }

            _ = this._currentCulture.TryRemove(channel, out _);
            _ = this._currentCulture.TryAdd(channel, newCulture);

            OnCultureChanged?.Invoke(this, args);
        }
    }

    #endregion Private Methods
}