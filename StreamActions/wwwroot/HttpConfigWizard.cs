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

/*
 * using MongoDB.Driver;
using StreamActions.Database;
using StreamActions.Database.Documents.Users;
using StreamActions.JsonDocuments;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.Users;
using TwitchLib.Api.ThirdParty.AuthorizationFlow;

namespace StreamActions.Http.ConfigWizard
{
    /// <summary>
    /// Handles the HTTP configuration wizard.
    /// </summary>
    internal static class HttpConfigWizard
    {
        /// <summary>
        /// Handles requests for the HTTP configuration wizard.
        /// </summary>
        /// <param name="request">The <see cref="HttpServerRequestMessage"/> representing the request.</param>
        internal static async void HandleRequest(HttpServerRequestMessage request)
        {
            if (request.Method == HttpMethod.Get || request.Method == HttpMethod.Put)
            {
                await HandleGet(request).ConfigureAwait(false);
            }
            else
            {
                await HttpServer.SendHTTPResponseAsync(request.TcpClient, request.Stream, HttpStatusCode.MethodNotAllowed, Array.Empty<byte>()).ConfigureAwait(false);
            }
        }

        private static readonly List<AuthScopes> _broadcasterAuthScopes = new List<AuthScopes> { AuthScopes.Channel_Check_Subscription, AuthScopes.Channel_Commercial, AuthScopes.Channel_Editor, AuthScopes.Channel_Read, AuthScopes.Channel_Stream, AuthScopes.Channel_Subscriptions, AuthScopes.Helix_Bits_Read, AuthScopes.Helix_Channel_Read_Subscriptions, AuthScopes.Helix_Moderation_Read, AuthScopes.Helix_User_Edit, AuthScopes.Helix_User_Edit_Broadcast, AuthScopes.Helix_User_Read_Email, AuthScopes.User_Read, AuthScopes.User_Subscriptions };
        private static readonly List<AuthScopes> _chatAuthScopes = new List<AuthScopes> { AuthScopes.Chat_Login };
        private static readonly Regex _oauthRegex = new Regex(@"^[a-zA-Z0-9]+$", RegexOptions.Compiled);
        private static readonly Regex _usernameRegex = new Regex(@"^[a-zA-Z0-9_]{4,25}$", RegexOptions.Compiled);

        private static async Task HandleGet(HttpServerRequestMessage request)
        {
            string contentType = "text/html";
            string sContent;

            if (request.RequestUri.AbsolutePath.EndsWith("ConnectWithTwitch.json", StringComparison.OrdinalIgnoreCase))
            {
                contentType = "application/json";
                using MemoryStream stream = new MemoryStream();
                using Utf8JsonWriter writer = new Utf8JsonWriter(stream);

                writer.WriteStartObject();

                if (request.QueryParams.ContainsKey("type"))
                {
                    switch (request.QueryParams["type"][0])
                    {
                        case "bot":
                            CreatedFlow bflow = Program.TwitchApi.ThirdParty.AuthorizationFlow.CreateFlow("StreamActions (Bot)", _broadcasterAuthScopes.Concat(_chatAuthScopes));
                            Program.TwitchApi.ThirdParty.AuthorizationFlow.BeginPingingStatus(bflow.Id);
                            writer.WriteString("Id", bflow.Id);
                            writer.WriteString("Url", bflow.Url);
                            break;

                        case "broadcaster":
                            CreatedFlow cflow = Program.TwitchApi.ThirdParty.AuthorizationFlow.CreateFlow("StreamActions (Broadcaster)", _broadcasterAuthScopes);
                            Program.TwitchApi.ThirdParty.AuthorizationFlow.BeginPingingStatus(cflow.Id);
                            writer.WriteString("Id", cflow.Id);
                            writer.WriteString("Url", cflow.Url);
                            break;

                        case "check":
                        case "check-bot":
                            PingResponse response = request.QueryParams.ContainsKey("id")
                                ? Program.TwitchApi.ThirdParty.AuthorizationFlow.PingStatus(request.QueryParams["id"][0])
                                : Program.TwitchApi.ThirdParty.AuthorizationFlow.PingStatus();

                            writer.WriteString("Id", response.Id);

                            if (response.Success)
                            {
                                if (request.QueryParams["type"][0] == "check-bot")
                                {
                                    Program.Settings.BotOAuth = response.Token;
                                    Program.Settings.BotRefreshToken = response.Refresh;
                                    Program.TwitchApi.Settings.AccessToken = response.Token;
                                }

                                writer.WriteString("Token", response.Token);
                                writer.WriteString("Refresh", response.Refresh);
                            }

                            writer.WriteNumber("Error", response.Error);
                            writer.WriteString("Message", response.Message);
                            break;

                        default:
                            break;
                    }
                }

                writer.WriteEndObject();

                sContent = Encoding.UTF8.GetString(stream.ToArray());
            }
            else if (request.RequestUri.AbsolutePath.EndsWith("CheckUsername.json", StringComparison.OrdinalIgnoreCase))
            {
                contentType = "application/json";
                using MemoryStream stream = new MemoryStream();
                using Utf8JsonWriter writer = new Utf8JsonWriter(stream);

                writer.WriteStartObject();

                if (request.QueryParams.ContainsKey("username") && !string.IsNullOrWhiteSpace(request.QueryParams["username"][0]))
                {
                    GetUsersResponse response = await Program.TwitchApi.Helix.Users.GetUsersAsync(null, new List<string> { request.QueryParams["username"][0] }).ConfigureAwait(false);

                    writer.WriteBoolean("valid", response.Users.Length > 0);
                }

                writer.WriteEndObject();

                sContent = Encoding.UTF8.GetString(stream.ToArray());
            }
            else
            {
                string resource = "ConfigWizard.html";
                if (request.RequestUri.AbsolutePath.EndsWith("TwitchConnect.png", StringComparison.OrdinalIgnoreCase))
                {
                    resource = "TwitchConnect.png";
                    contentType = "image/png";
                }

                using StreamReader reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("StreamActions.Http.ConfigWizard." + resource));
                sContent = await reader.ReadToEndAsync().ConfigureAwait(false);
            }

            if (request.Method == HttpMethod.Post)
            {
                sContent = HandlePost(request, sContent);
            }

            sContent = await UpdateFormInputsAsync(sContent).ConfigureAwait(false);

            sContent = HttpServer.DoI18N(sContent, new Dictionary<string, object>());

            byte[] content = Encoding.UTF8.GetBytes(sContent);
            Dictionary<string, List<string>> headers = new Dictionary<string, List<string>>
            {
                { "Content-Length", new List<string> { content.Length.ToString(CultureInfo.InvariantCulture) } },
                { "Content-Type", new List<string> { contentType } }
            };

            await HttpServer.SendHTTPResponseAsync(request.TcpClient, request.Stream, HttpStatusCode.OK, content, headers).ConfigureAwait(false);
        }

        private static string HandlePost(HttpServerRequestMessage request, string sContent)
        {
            //TODO: Update Settings
            List<string> errors = new List<string>();

            if (request.PostParams.TryGetValue("BotLogin", out List<string> values))
            {
                string value = values[0];
                if (_usernameRegex.IsMatch(value))
                {
                    if (value != Program.Settings.BotLogin)
                    {
                        Program.Settings.BotLogin = value.Trim();
                    }
                }
                else if (string.IsNullOrWhiteSpace(value))
                {
                    errors.Add("Bot Username is required");
                }
                else if (value.Length < 4 || value.Length > 25)
                {
                    errors.Add("Bot Username is an invalid length");
                }
                else
                {
                    errors.Add("Bot Username contains invalid characters");
                }
            }
            else
            {
                errors.Add("Bot Username is required");
            }

            if (request.PostParams.TryGetValue("BotOAuth", out values))
            {
                string value = values[0];
                value = value.Replace("oauth:", "", StringComparison.OrdinalIgnoreCase);
                if (_oauthRegex.IsMatch(value))
                {
                    if (value != Program.Settings.BotOAuth)
                    {
                        Program.Settings.BotOAuth = value.Trim();
                    }
                }
                else if (string.IsNullOrWhiteSpace(value))
                {
                    errors.Add("Bot OAuth is required");
                }
                else
                {
                    errors.Add("Bot OAuth contains invalid characters");
                }
            }
            else
            {
                errors.Add("Bot OAuth is required");
            }

            if (request.PostParams.TryGetValue("BotRefreshToken", out values))
            {
                string value = values[0];
                if (_oauthRegex.IsMatch(value))
                {
                    if (value != Program.Settings.BotRefreshToken)
                    {
                        Program.Settings.BotRefreshToken = value.Trim();
                    }
                }
                else if (string.IsNullOrWhiteSpace(value))
                {
                    errors.Add("Bot Refresh Token is required");
                }
                else
                {
                    errors.Add("Bot Refresh Token contains invalid characters");
                }
            }
            else
            {
                errors.Add("Bot Refresh Token is required");
            }

            if (request.PostParams.TryGetValue("ChatCommandIdentifier", out values))
            {
                string value = values[0];
                if (!string.IsNullOrWhiteSpace(value) && value.Length == 1)
                {
                    if (value[0] != Program.Settings.ChatCommandIdentifier)
                    {
                        Program.Settings.ChatCommandIdentifier = value.Trim()[0];
                    }
                }
                else
                {
                    errors.Add("Chat Command Identifier must be exactly 1 character");
                }
            }
            else
            {
                if (Program.Settings.ChatCommandIdentifier == (char)0)
                {
                    Program.Settings.ChatCommandIdentifier = '!';
                }
            }

            if (request.PostParams.TryGetValue("WhisperCommandIdentifier", out values))
            {
                string value = values[0];
                if (!string.IsNullOrWhiteSpace(value) && value.Length == 1)
                {
                    if (value[0] != Program.Settings.WhisperCommandIdentifier)
                    {
                        Program.Settings.WhisperCommandIdentifier = value.Trim()[0];
                    }
                }
                else
                {
                    errors.Add("Whisper Command Identifier must be exactly 1 character");
                }
            }
            else
            {
                if (Program.Settings.WhisperCommandIdentifier == (char)0)
                {
                    Program.Settings.WhisperCommandIdentifier = '!';
                }
            }

            if (request.PostParams.TryGetValue("GlobalCulture", out values))
            {
                string value = values[0];
                try
                {
                    _ = new CultureInfo(value, false);
                    if (value != Program.Settings.GlobalCulture)
                    {
                        Program.Settings.GlobalCulture = value.Trim();
                    }
                }
                catch (CultureNotFoundException)
                {
                    errors.Add("The specified culture is invalid or not available on this system");
                }
            }

            IEnumerable<dynamic> channels = request.PostParams.AsEnumerable().Where(kvp => kvp.Key.StartsWith("ChannelsToJoin", StringComparison.Ordinal) && kvp.Key.Contains("[", StringComparison.Ordinal) && kvp.Key.Contains("]", StringComparison.Ordinal)).GroupBy(kvp => kvp.Key.Substring(kvp.Key.IndexOf("[", StringComparison.Ordinal) + 1, kvp.Key.IndexOf("]", StringComparison.Ordinal) - kvp.Key.IndexOf("[", StringComparison.Ordinal) - 1), (channel, data) =>
            {
                string channelLogin = "";
                string channelOAuth = "";
                string channelRefresh = "";

                foreach (KeyValuePair<string, List<string>> kvp in data)
                {
                    if (kvp.Key == "ChannelsToJoin[" + channel + "]")
                    {
                        channelLogin = kvp.Value[0].Trim();
                    }
                    else if (kvp.Key == "ChannelsToJoinOAuth[" + channel + "]")
                    {
                        channelOAuth = kvp.Value[0].Replace("oauth:", "", StringComparison.OrdinalIgnoreCase).Trim();
                    }
                    else if (kvp.Key == "ChannelsToJoinRefresh[" + channel + "]")
                    {
                        channelRefresh = kvp.Value[0].Trim();
                    }
                }

                return new
                {
                    Channel = channelLogin,
                    OAuth = channelOAuth,
                    Refresh = channelRefresh
                };
            });

            Program.Settings.ChannelsToJoin.Clear();

            foreach (dynamic channel in channels)
            {
                // TODO: Validate against Twitch API and get ID
                _ = _usernameRegex.IsMatch(channel.Channel)
                    ? Program.Settings.ChannelsToJoin.Add(channel.Channel)
                    : channel.Channel.Length < 4 || channel.Channel.Length > 25
                        ? errors.Add("Channel name <i>" + channel.Channel + "</i> is an invalid length")
                        : errors.Add("Channel name <i>" + channel.Channel + "</i> contains invalid characters");

                // TODO: Add user and tokens to MongoDB, if required
            }

            using FileStream fs = File.OpenWrite(Path.GetFullPath(Path.Combine(typeof(Program).Assembly.Location, "settings.json")));
            using Task t = JsonSerializer.SerializeAsync<SettingsDocument>(fs, Program.Settings);
            t.Start();

            if (errors.Count > 0)
            {
                string errOut = "";
                foreach (string err in errors)
                {
                    if (errOut.Length > 0)
                    {
                        errOut += "<br />";
                    }

                    errOut += err;
                }

                sContent = sContent.Replace("/*ResponseTextValue/", " + \"" + errOut.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"", StringComparison.Ordinal);
            }

            return sContent;
        }

        private static async Task<string> UpdateFormInputsAsync(string sContent)
        {
            sContent = sContent.Replace("BotLoginValue", Program.Settings.BotLogin, StringComparison.Ordinal)
                .Replace("BotOAuthValue", Program.Settings.BotOAuth, StringComparison.Ordinal)
                .Replace("BotRefreshTokenValue", Program.Settings.BotRefreshToken, StringComparison.Ordinal)
                .Replace("ChatCommandIdentifierValue", Program.Settings.ChatCommandIdentifier.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("WhisperCommandIdentifierValue", Program.Settings.WhisperCommandIdentifier.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("GlobalCultureValue", Program.Settings.GlobalCulture, StringComparison.Ordinal)
                .Replace("DBHostValue", Program.Settings.DBHost, StringComparison.Ordinal)
                .Replace("DBPortValue", Program.Settings.DBPort.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("DBNameValue", Program.Settings.DBName, StringComparison.Ordinal)
                .Replace("DBUsernameValue", Program.Settings.DBUsername, StringComparison.Ordinal)
                .Replace("DBPasswordValue", Program.Settings.DBPassword, StringComparison.Ordinal)
                .Replace("WsListenIPValue", Program.Settings.WSListenIp, StringComparison.Ordinal)
                .Replace("WsListenPortValue", Program.Settings.WSListenPort.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("WsSuperadminBearerValue", Program.Settings.WSSuperadminBearer, StringComparison.Ordinal)
                .Replace("WsSslCertValue", Program.Settings.WSSslCert, StringComparison.Ordinal)
                .Replace("WsSslCertPassValue", Program.Settings.WSSslCertPass, StringComparison.Ordinal);

            if (Program.Settings.DBUseTls)
            {
                sContent = sContent.Replace("id=\"DBUseTls\"", "id=\"DBUseTls\" checked=\"checked\"", StringComparison.Ordinal);
            }

            if (Program.Settings.DBAllowInsecureTls)
            {
                sContent = sContent.Replace("id=\"DBAllowInsecureTls\"", "id=\"DBAllowInsecureTls\" checked=\"checked\"", StringComparison.Ordinal);
            }

            if (Program.Settings.WSUseSsl)
            {
                sContent = sContent.Replace("id=\"WsUseSsl\"", "id=\"WsUseSsl\" checked=\"checked\"", StringComparison.Ordinal);
            }

            if (Program.Settings.ShowDebugMessages)
            {
                sContent = sContent.Replace("id=\"ShowDebugMessages\"", "id=\"ShowDebugMessages\" checked=\"checked\"", StringComparison.Ordinal);
            }

            if (Program.Settings.SendExceptions)
            {
                sContent = sContent.Replace("id=\"SendExceptions\"", "id=\"SendExceptions\" checked=\"checked\"", StringComparison.Ordinal);
            }

            string channelJoinList = "";

            if (Program.Settings.ChannelsToJoin.Count > 0)
            {
                IMongoCollection<UserDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<UserDocument>(UserDocument.CollectionName);
                IMongoCollection<AuthenticationDocument> collection2 = DatabaseClient.Instance.MongoDatabase.GetCollection<AuthenticationDocument>(AuthenticationDocument.CollectionName);

                foreach (string channel in Program.Settings.ChannelsToJoin)
                {
                    string oauth = "";
                    string refresh = "";

                    FilterDefinition<UserDocument> filter = Builders<UserDocument>.Filter.Where(d => d.Login.Equals(channel, StringComparison.OrdinalIgnoreCase));
                    if (await collection.CountDocumentsAsync(filter).ConfigureAwait(false) > 0)
                    {
                        using IAsyncCursor<UserDocument> cursor = await collection.FindAsync(filter).ConfigureAwait(false);

                        UserDocument userDocument = await cursor.SingleAsync().ConfigureAwait(false);

                        FilterDefinition<AuthenticationDocument> filter2 = Builders<AuthenticationDocument>.Filter.Where(d => d.UserId.Equals(userDocument.Id, StringComparison.OrdinalIgnoreCase));

                        if (await collection2.CountDocumentsAsync(filter2).ConfigureAwait(false) > 0)
                        {
                            using IAsyncCursor<AuthenticationDocument> cursor2 = await collection2.FindAsync(filter2).ConfigureAwait(false);

                            AuthenticationDocument authenticationDocument = await cursor2.SingleAsync().ConfigureAwait(false);

                            if (!string.IsNullOrWhiteSpace(authenticationDocument.TwitchToken.Token))
                            {
                                oauth = authenticationDocument.TwitchToken.Token;
                            }

                            if (!string.IsNullOrWhiteSpace(authenticationDocument.TwitchToken.Refresh))
                            {
                                refresh = authenticationDocument.TwitchToken.Refresh;
                            }
                        }
                    }

                    channelJoinList += "<tr id=\"ChannelList[" + channel + "]\">";

                    channelJoinList += "<td id=\"ChannelsToJoinNameTd[" + channel + "]\">";
                    channelJoinList += "<span style=\"font-weight: bold;\">" + channel + "</span>";
                    channelJoinList += "<input type=\"hidden\" name=\"ChannelsToJoin[" + channel + "]\" value=\"" + channel + "\" />";
                    channelJoinList += "</td>";

                    channelJoinList += "<td id=\"ChannelsToJoinOAuthTd[" + channel + "]\" style=\"padding-left: 15px;\">";
                    channelJoinList += "<label for=\"ChannelsToJoinOAuth[" + channel + "]\">Broadcaster OAuth: </label>";
                    channelJoinList += "<input type=\"text\" name=\"ChannelsToJoinOAuth[" + channel + "]\" value=\"" + oauth + "\" />";
                    channelJoinList += "</td>";

                    channelJoinList += "<td id=\"ChannelsToJoinRefreshTd[" + channel + "]\" style=\"padding-left: 15px;\">";
                    channelJoinList += "<label for=\"ChannelsToJoinRefresh[" + channel + "]\">Refresh Token: </label>";
                    channelJoinList += "<input type=\"text\" name=\"ChannelsToJoinRefresh[" + channel + "]\" value=\"" + refresh + "\" />";
                    channelJoinList += "</td>";

                    channelJoinList += "<td id=\"ChannelsToJoinActionsTd[" + channel + "]\" style=\"padding-left: 15px;\">";
                    channelJoinList += "<img id=\"ChannelsToJoinActionsConnect[" + channel + "]\" alt=\"Connect with Twitch\" src=\"/config/TwitchConnect.png\" style=\"cursor: pointer; position: relative; top: 3px;\" />";
                    channelJoinList += "<input type=\"button\" id=\"ChannelsToJoinActionsRemove[" + channel + "]\" value=\"Remove\" style=\"margin-left: 15px; cursor: pointer; position: relative; bottom: 8px;\" />";
                    channelJoinList += "</td>";

                    channelJoinList += "</tr>";
                }
            }

            sContent = sContent.Replace("<!--ChannelJoinList-->", channelJoinList, StringComparison.Ordinal);

            return sContent;
        }
    }
}
*/
