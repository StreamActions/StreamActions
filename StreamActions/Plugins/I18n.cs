/*
 * Copyright © 2019-2021 StreamActions Team
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

using MongoDB.Driver;
using StreamActions.Attributes;
using StreamActions.Database;
using StreamActions.Database.Documents.Users;
using StreamActions.Enums;
using StreamActions.EventArgs;
using StreamActions.Interfaces.Plugin;
using StreamActions.JsonDocuments;
using StreamActions.Plugin;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace StreamActions.Plugins
{
    /// <summary>
    /// Handles I18n tasks.
    /// </summary>
    [Guid("53739937-794D-406F-AFF3-5340F96C4D50")]
    public class I18n : IPlugin
    {
        #region Public Fields

        /// <summary>
        /// The default Culture.
        /// </summary>
        public static readonly string DefaultCulture = "en-US";

        #endregion Public Fields

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
        public IReadOnlyCollection<Guid> Dependencies => ImmutableArray<Guid>.Empty;
        public CultureInfo GlobalCulture => this._globalCulture;

        /// <summary>
        /// A Dictionary of currently loaded I18nDocuments
        /// </summary>
        public ReadOnlyDictionary<string, I18nDocument> I18nDocuments => new ReadOnlyDictionary<string, I18nDocument>(this._i18nDocuments);

        public string PluginAuthor => "StreamActions Team";

        public string PluginDescription => "Provides i18n support";

        public Guid PluginId => typeof(I18n).GUID;
        public string PluginName => "I18n";
        public Uri PluginUri => new Uri("https://github.com/StreamActions/StreamActions");
        public string PluginVersion => "1.0.0";

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Formats a string with the provided replacements.
        /// </summary>
        /// <param name="format">A standard format string, as used with <c>string.Format</c>, except named replacements (eg. <c>{MyValue}</c>) are valid.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="source">A type that provides the replacement values. Can be <c>Dictionary&lt;string, string&gt;</c>, <c>List&lt;string&gt;</c>, or an anonymous type (eg. <c>new { MyValue = "hello!" }</c>).</param>
        /// <returns>A copy of <paramref name="format"/> in which the format items have been replaced by the string representation of the corresponding objects in <paramref name="source"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="format"/> or <paramref name="source"/> is null.</exception>
        public static string FormatWith(string format, IFormatProvider provider, object source)
        {
            if (format is null)
            {
                throw new ArgumentNullException(nameof(format));
            }

            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            List<object> values = new List<object>();
            Dictionary<string, object> dsource;

            if (source is Dictionary<string, string> dictionary)
            {
                dsource = dictionary.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value);
            }
            else if (source is List<string> list)
            {
                int i = 0;
                dsource = list.ToDictionary(_ => i++.ToString(CultureInfo.InvariantCulture), s => (object)s);
            }
            else
            {
                dsource = source.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(source, null));
            }

            string rewrittenFormat = _formatWithRegex.Replace(format, (m) =>
            {
                Group startGroup = m.Groups["start"];
                Group propertyGroup = m.Groups["property"];
                Group formatGroup = m.Groups["format"];
                Group endGroup = m.Groups["end"];

                values.Add(dsource.ContainsKey(propertyGroup.Value) ? dsource[propertyGroup.Value] : "{" + propertyGroup.Value + "}");

                return new string('{', startGroup.Captures.Count) + (values.Count - 1) + formatGroup.Value + new string('}', endGroup.Captures.Count);
            });

            return string.Format(provider, rewrittenFormat, values.ToArray());
        }

        /// <summary>
        /// Attempts to retrieve the specified i18n replacement string.
        /// </summary>
        /// <param name="category">The category to select from.</param>
        /// <param name="key">The string key to select.</param>
        /// <param name="document">The <see cref="I18nDocument"/> to use.</param>
        /// <param name="defVal">The default value to return if either the <paramref name="category"/> or <paramref name="key"/> do not exist; <c>null</c> if not provided</param>
        /// <returns>The i18n replacement string, if present; <paramref name="defVal"/> otherwise.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="document"/> is null.</exception>
        public static string Get(string category, string key, I18nDocument document, string defVal = null)
        {
            if (document is null)
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
        /// Attempts to retrieve the specified i18n replacement string, and then formats it.
        /// </summary>
        /// <param name="category">The category to select from.</param>
        /// <param name="key">The string key to select.</param>
        /// <param name="document">The <see cref="I18nDocument"/> to use.</param>
        /// <param name="source">A type that provides the replacement values. Can be <c>Dictionary&lt;string, string&gt;</c>, <c>List&lt;string&gt;</c>, or an anonymous type (eg. <c>new { MyValue = "hello!" }</c>).</param>
        /// <param name="culture">The culture to use.</param>
        /// <param name="defVal">The default value to return if either the <paramref name="category"/> or <paramref name="key"/> do not exist; <c>null</c> if not provided</param>
        /// <returns>A copy of the i18n replacement string (or <paramref name="defVal"/> if it is not found) in which the format items have been replaced by the string representation of the corresponding objects in <paramref name="source"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="document"/> is null.</exception>
        public static string GetAndFormatWith(string category, string key, I18nDocument document, object source, CultureInfo culture, string defVal = null) => FormatWith(Get(category, key, document, defVal), culture, source);

        /// <summary>
        /// Attempts to retrieve the specified i18n replacement string, and then formats it, using the global culture.
        /// </summary>
        /// <param name="category">The category to select from.</param>
        /// <param name="key">The string key to select.</param>
        /// <param name="source">A type that provides the replacement values. Can be <c>Dictionary&lt;string, string&gt;</c>, <c>List&lt;string&gt;</c>, or an anonymous type (eg. <c>new { MyValue = "hello!" }</c>).</param>
        /// <param name="defVal">The default value to return if either the <paramref name="category"/> or <paramref name="key"/> do not exist; <c>null</c> if not provided</param>
        /// <returns>A copy of the i18n replacement string (or <paramref name="defVal"/> if it is not found) in which the format items have been replaced by the string representation of the corresponding objects in <paramref name="source"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="document"/> is null.</exception>
        public static string GetAndFormatWith(string category, string key, object source, string defVal = null) => FormatWith(Get(category, key, I18n.Instance.I18nDocuments.GetValueOrDefault(I18n.Instance.GlobalCulture.Name, I18nDocument.Empty), defVal), I18n.Instance.GlobalCulture, source);

        /// <summary>
        /// Loads the i18n files for the specified culture and merges it all into a single <see cref="I18nDocument"/>.
        /// The files must be under the <c>i18n/lang-region</c> folder, such as <c>i18n/en-US</c> or <c>i18n/es-ES</c> and have <c>.json</c> extensions.
        /// </summary>
        /// <param name="newCulture">The culture to load.</param>
        /// <returns>An <see cref="I18nDocument"/> containing the merged contents of all valid json files.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="newCulture"/> is null.</exception>
        public static async Task<I18nDocument> LoadCultureAsync(CultureInfo newCulture)
        {
            if (newCulture is null)
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
                        document.Merge(await JsonSerializer.DeserializeAsync<I18nDocument>(fs).ConfigureAwait(false));
                    }
                    catch (JsonException) { }
                }
            }

            return document;
        }

        public void Disabled()
        {
        }

        public void Enabled() => this.ChangeGlobalCultureAsync(Program.Settings.GlobalCulture);

        /// <summary>
        /// Attempts to retrieve the specified i18n replacement string.
        /// </summary>
        /// <param name="category">The category to select from.</param>
        /// <param name="key">The string key to select.</param>
        /// <param name="culture">The CultureInfo that should be used.</param>
        /// <param name="defVal">The default value to return if either the <paramref name="category"/> or <paramref name="key"/> do not exist; <c>null</c> if not provided</param>
        /// <returns>The i18n replacement string, if present; <paramref name="defVal"/> otherwise.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="culture"/> is null.</exception>
        public string Get(string category, string key, CultureInfo culture, string defVal = null) => culture is null
                ? throw new ArgumentNullException(nameof(culture))
                : Get(category, key, this._i18nDocuments.GetValueOrDefault(culture.Name, I18nDocument.Empty), defVal);

        /// <summary>
        /// Attempts to retrieve the specified i18n replacement string, and then formats it.
        /// </summary>
        /// <param name="category">The category to select from.</param>
        /// <param name="key">The string key to select.</param>
        /// <param name="culture">The CultureInfo that should be used.</param>
        /// <param name="source">A type that provides the replacement values. Can be <c>Dictionary&lt;string, string&gt;</c>, <c>List&lt;string&gt;</c>, or an anonymous type (eg. <c>new { MyValue = "hello!" }</c>).</param>
        /// <param name="defVal">The default value to return if either the <paramref name="category"/> or <paramref name="key"/> do not exist; <c>null</c> if not provided</param>
        /// <returns>A copy of the i18n replacement string (or <paramref name="defVal"/> if it is not found) in which the format items have been replaced by the string representation of the corresponding objects in <paramref name="source"/>.</returns>
        public string GetAndFormatWith(string category, string key, CultureInfo culture, object source, string defVal = null) => FormatWith(this.Get(category, key, culture, defVal), culture, source);

        /// <summary>
        /// Attempts to retrieve the specified i18n replacement string, and then formats it.
        /// </summary>
        /// <param name="category">The category to select from.</param>
        /// <param name="key">The string key to select.</param>
        /// <param name="channelId">The channelId whose CurrentCulture should be used.</param>
        /// <param name="source">A type that provides the replacement values. Can be <c>Dictionary&lt;string, string&gt;</c>, <c>List&lt;string&gt;</c>, or an anonymous type (eg. <c>new { MyValue = "hello!" }</c>).</param>
        /// <param name="defVal">The default value to return if either the <paramref name="category"/> or <paramref name="key"/> do not exist; <c>null</c> if not provided</param>
        /// <returns>A copy of the i18n replacement string (or <paramref name="defVal"/> if it is not found) in which the format items have been replaced by the string representation of the corresponding objects in <paramref name="source"/>.</returns>
        public async Task<string> GetAndFormatWithAsync(string category, string key, string channelId, object source, string defVal = null) => FormatWith(await this.GetAsync(category, key, channelId, defVal).ConfigureAwait(false), await this.GetCurrentCultureAsync(channelId).ConfigureAwait(false), source);

        /// <summary>
        /// Attempts to retrieve the specified i18n replacement string.
        /// </summary>
        /// <param name="category">The category to select from.</param>
        /// <param name="key">The string key to select.</param>
        /// <param name="channelId">The channelId whose CurrentCulture should be used.</param>
        /// <param name="defVal">The default value to return if either the <paramref name="category"/> or <paramref name="key"/> do not exist; <c>null</c> if not provided</param>
        /// <returns>The i18n replacement string, if present; <paramref name="defVal"/> otherwise.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="channelId"/> is null.</exception>
        public async Task<string> GetAsync(string category, string key, string channelId, string defVal = null) => channelId is null
                ? throw new ArgumentNullException(nameof(channelId))
                : this.Get(category, key, await this.GetCurrentCultureAsync(channelId).ConfigureAwait(false), defVal);

        /// <summary>
        /// Gets the current CultureInfo of the specified channelId, and ensures the cultures I18n data is loaded.
        /// </summary>
        /// <param name="channelId">The channelId to check.</param>
        /// <returns>The current CultureInfo of the channel; <see cref="GlobalCulture"/> if the channel is not found or hasn't set a culture.</returns>
        public async Task<CultureInfo> GetCurrentCultureAsync(string channelId)
        {
            string currentCulture = await this.GetCurrentCultureNameAsync(channelId).ConfigureAwait(false) ?? this.GlobalCulture.Name;

            await this.LoadCultureIfNeededAsync(currentCulture).ConfigureAwait(false);

            return this._loadedCultures[currentCulture];
        }

        /// <summary>
        /// Gets the name of the current culture for the specified channel.
        /// </summary>
        /// <param name="channelId">The channelId to check.</param>
        /// <returns>The name of the current culture; <see cref="GlobalCulture"/> if the channel is not found or hasn't set a culture.</returns>
        public async Task<string> GetCurrentCultureNameAsync(string channelId)
        {
            IMongoCollection<UserDocument> users = DatabaseClient.Instance.MongoDatabase.GetCollection<UserDocument>(UserDocument.CollectionName);
            FilterDefinition<UserDocument> filter = Builders<UserDocument>.Filter.Eq(u => u.Id, channelId);
            using IAsyncCursor<string> cursor = await users.FindAsync(filter, new FindOptions<UserDocument, string> { Projection = Builders<UserDocument>.Projection.Expression(d => d.CurrentCulture) }).ConfigureAwait(false);

            return await cursor.SingleOrDefaultAsync().ConfigureAwait(false) ?? this.GlobalCulture.Name;
        }

        public string GetCursor() => this.PluginId.ToString("D", CultureInfo.InvariantCulture);

        /// <summary>
        /// Loads the specified culture and it's I18n data into memory, if needed.
        /// </summary>
        /// <param name="newCulture">The CultureInfo to load.</param>
        /// <returns>A Task that can be awaited</returns>
        /// <exception cref="ArgumentNullException"><paramref name="newCulture"/> is null.</exception>
        public async Task LoadCultureIfNeededAsync(CultureInfo newCulture)
        {
            if (newCulture is null)
            {
                throw new ArgumentNullException(nameof(newCulture));
            }

            if (!this._loadedCultures.ContainsKey(newCulture.Name))
            {
                _ = this._loadedCultures.TryAdd(newCulture.Name, newCulture);
            }

            if (!this._i18nDocuments.ContainsKey(newCulture.Name))
            {
                _ = this._i18nDocuments.TryAdd(newCulture.Name, await LoadCultureAsync(newCulture).ConfigureAwait(false));
            }
        }

        /// <summary>
        /// Loads the specified culture and it's I18n data into memory, if needed.
        /// </summary>
        /// <param name="culture">The name of the culture to load.</param>
        /// <param name="useUseroverride">A bool that denotes whether to use the user-selected culture settings (<c>true</c>) or the default culture settings (<c>false</c>).</param>
        /// <returns>A Task that can be awaited</returns>
        /// <exception cref="ArgumentNullException"><paramref name="culture"/> is null.</exception>
        /// <exception cref="CultureNotFoundException"><paramref name="culture"/> is not a valid culture name.</exception>
        public async Task LoadCultureIfNeededAsync(string culture, bool useUserOverride = false)
        {
            if (culture is null)
            {
                throw new ArgumentNullException(nameof(culture));
            }

            CultureInfo newCulture = new CultureInfo(culture, useUserOverride);

            await this.LoadCultureIfNeededAsync(newCulture).ConfigureAwait(false);
        }

        #endregion Public Methods

        #region Private Fields

        /// <summary>
        /// Regex for <see cref="FormatWith(string, IFormatProvider, dynamic)"/>.
        /// </summary>
        private static readonly Regex _formatWithRegex = new Regex(@"(?<start>\{)+(?<property>[\w\.\[\]]+)(?<format>:[^}]+)?(?<end>\})+", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        /// <summary>
        /// An <see cref="I18nDocument"/> containing all currently loaded i18n data.
        /// </summary>
        private readonly ConcurrentDictionary<string, I18nDocument> _i18nDocuments = new ConcurrentDictionary<string, I18nDocument>();

        /// <summary>
        /// Contains loaded cultures.
        /// </summary>
        private readonly ConcurrentDictionary<string, CultureInfo> _loadedCultures = new ConcurrentDictionary<string, CultureInfo>();

        /// <summary>
        /// Field that backs <see cref="GlobalCulture"/>.
        /// </summary>
        private CultureInfo _globalCulture = new CultureInfo(DefaultCulture, false);

        #endregion Private Fields

        #region Private Methods

        /// <summary>
        /// Changes the current culture to the specified one.
        /// </summary>
        /// <param name="channelId">The channelId whose culture should be changed.</param>
        /// <param name="culture">The <c>languagecode2-country/regioncode2</c> name of the new culture to load.</param>
        /// <param name="useUseroverride">A bool that denotes whether to use the user-selected culture settings (<c>true</c>) or the default culture settings (<c>false</c>).</param>
        /// <exception cref="CultureNotFoundException"><paramref name="culture"/> is not a valid culture name.</exception>
        private async void ChangeCultureAsync(string channelId, string culture, bool useUserOverride = false)
        {
            CultureInfo newCulture = new CultureInfo(culture, useUserOverride);
            I18nDocument oldDocument = this._i18nDocuments.GetValueOrDefault(await this.GetCurrentCultureNameAsync(channelId).ConfigureAwait(false), I18nDocument.Empty);

            OnCultureChangedArgs args = new OnCultureChangedArgs
            {
                Channel = channelId,
                OldCulture = await this.GetCurrentCultureAsync(channelId).ConfigureAwait(false),
                OldI18nDocument = new WeakReference<I18nDocument>(oldDocument)
            };

            if (!this._i18nDocuments.ContainsKey(newCulture.Name))
            {
                _ = this._i18nDocuments.TryAdd(newCulture.Name, await LoadCultureAsync(newCulture).ConfigureAwait(false));
            }

            if (!this._loadedCultures.ContainsKey(newCulture.Name))
            {
                _ = this._loadedCultures.TryAdd(newCulture.Name, newCulture);
            }

            IMongoCollection<UserDocument> users = DatabaseClient.Instance.MongoDatabase.GetCollection<UserDocument>(UserDocument.CollectionName);
            UpdateDefinition<UserDocument> userUpdate = Builders<UserDocument>.Update.Set(u => u.CurrentCulture, newCulture.Name);
            FilterDefinition<UserDocument> filter = Builders<UserDocument>.Filter.Eq(u => u.Id, channelId);
            _ = await users.UpdateOneAsync(filter, userUpdate).ConfigureAwait(false);

            OnCultureChanged?.Invoke(this, args);
        }

        /// <summary>
        /// Changes the current global culture to the specified one.
        /// </summary>
        /// <param name="culture">The <c>languagecode2-country/regioncode2</c> name of the new culture to load.</param>
        /// <param name="useUseroverride">A bool that denotes whether to use the user-selected culture settings (<c>true</c>) or the default culture settings (<c>false</c>).</param>
        /// <exception cref="CultureNotFoundException"><paramref name="culture"/> is not a valid culture name.</exception>
        private async void ChangeGlobalCultureAsync(string culture, bool useUserOverride = false)
        {
            CultureInfo newCulture = new CultureInfo(culture, useUserOverride);
            I18nDocument oldDocument = this._i18nDocuments.GetValueOrDefault(this._globalCulture.Name, I18nDocument.Empty);

            OnCultureChangedArgs args = new OnCultureChangedArgs
            {
                Channel = "*",
                OldCulture = this._globalCulture,
                OldI18nDocument = new WeakReference<I18nDocument>(oldDocument)
            };

            if (!this._i18nDocuments.ContainsKey(newCulture.Name))
            {
                _ = this._i18nDocuments.TryAdd(newCulture.Name, await LoadCultureAsync(newCulture).ConfigureAwait(false));
            }

            await this.LoadCultureIfNeededAsync(newCulture).ConfigureAwait(false);

            this._globalCulture = newCulture;

            OnCultureChanged?.Invoke(this, args);
        }

        [BotnameChatCommand("culture", UserLevels.Broadcaster)]
        private async void I18n_OnI18nCommand(object sender, OnChatCommandReceivedArgs e)
        {
            if (e.Command.ArgumentsAsList.Count < 3 || !e.Command.ArgumentsAsList[1].Equals("set", StringComparison.OrdinalIgnoreCase))
            {
                TwitchLibClient.Instance.SendMessage(e.Command.ChatMessage.Channel, await this.GetAndFormatWithAsync("I18n", "CurrentCultureUsage", e.Command.ChatMessage.RoomId,
                    new
                    {
                        CommandPrefix = PluginManager.Instance.ChatCommandIdentifier,
                        CurrentCulture = await this.GetCurrentCultureNameAsync(e.Command.ChatMessage.RoomId).ConfigureAwait(false),
                        BotName = Program.Settings.BotLogin,
                        User = e.Command.ChatMessage.Username,
                        Sender = e.Command.ChatMessage.Username,
                        e.Command.ChatMessage.DisplayName
                    },
                    "@{DisplayName}, The current culture is set to {CurrentCulture}. You can change it with {CommandPrefix}{BotName} culture set [CultureName]").ConfigureAwait(false));
            }
            else
            {
                try
                {
                    this.ChangeCultureAsync(e.Command.ChatMessage.RoomId, e.Command.ArgumentsAsList[2]);

                    TwitchLibClient.Instance.SendMessage(e.Command.ChatMessage.Channel, await this.GetAndFormatWithAsync("I18n", "CurrentCultureChanged", e.Command.ChatMessage.RoomId,
                    new { CurrentCulture = await this.GetCurrentCultureNameAsync(e.Command.ChatMessage.RoomId).ConfigureAwait(false) },
                    "@{DisplayName}, The current culture was changed to {CurrentCulture}").ConfigureAwait(false));
                }
                catch (CultureNotFoundException)
                {
                    TwitchLibClient.Instance.SendMessage(e.Command.ChatMessage.Channel, await this.GetAsync("I18n", "CurrentCultureChangeFailed", e.Command.ChatMessage.RoomId,
                    "@{DisplayName}, The specified culture name is invalid in the current operating system environment").ConfigureAwait(false));
                }
            }
        }

        #endregion Private Methods
    }
}
