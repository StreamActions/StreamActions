using MongoDB.Driver;
using StreamActions.Database;
using StreamActions.Database.Documents.Users;
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
        #region Internal Methods

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

        #endregion Internal Methods

        #region Private Fields

        private static readonly List<AuthScopes> _broadcasterAuthScopes = new List<AuthScopes> { AuthScopes.Channel_Check_Subscription, AuthScopes.Channel_Commercial, AuthScopes.Channel_Editor, AuthScopes.Channel_Read, AuthScopes.Channel_Stream, AuthScopes.Channel_Subscriptions, AuthScopes.Helix_Bits_Read, AuthScopes.Helix_Channel_Read_Subscriptions, AuthScopes.Helix_Moderation_Read, AuthScopes.Helix_User_Edit, AuthScopes.Helix_User_Edit_Broadcast, AuthScopes.Helix_User_Read_Email, AuthScopes.User_Read, AuthScopes.User_Subscriptions };
        private static readonly List<AuthScopes> _chatAuthScopes = new List<AuthScopes> { AuthScopes.Chat_Login };

        #endregion Private Fields

        #region Private Methods

        private static async Task HandleGet(HttpServerRequestMessage request)
        {
            string contentType = "text/html";
            string sContent;
            Dictionary<string, string> queryParams = new Dictionary<string, string>();

            foreach (string s in request.RequestUri.Query.Split('&'))
            {
                string[] query = s.Split('=', 2);

                if (!string.IsNullOrWhiteSpace(query[0]) && !string.IsNullOrWhiteSpace(WebUtility.UrlDecode(query[1])))
                {
                    queryParams.Add(query[0].ToLowerInvariant(), WebUtility.UrlDecode(query[1]));
                }
            }

            if (request.RequestUri.AbsolutePath.EndsWith("ConnectWithTwitch.json", StringComparison.OrdinalIgnoreCase))
            {
                contentType = "application/json";
                using MemoryStream stream = new MemoryStream();
                using Utf8JsonWriter writer = new Utf8JsonWriter(stream);

                writer.WriteStartObject();

                if (queryParams.ContainsKey("type"))
                {
                    switch (queryParams["type"])
                    {
                        case "bot":
                            CreatedFlow bflow = Program.TwitchApi.ThirdParty.AuthorizationFlow.CreateFlow("StreamActions-Bot (Bot)", _broadcasterAuthScopes.Concat(_chatAuthScopes));
                            Program.TwitchApi.ThirdParty.AuthorizationFlow.BeginPingingStatus(bflow.Id);
                            writer.WriteString("Id", bflow.Id);
                            writer.WriteString("Url", bflow.Url);
                            break;

                        case "broadcaster":
                            CreatedFlow cflow = Program.TwitchApi.ThirdParty.AuthorizationFlow.CreateFlow("StreamActions-Bot (Broadcaster)", _broadcasterAuthScopes);
                            Program.TwitchApi.ThirdParty.AuthorizationFlow.BeginPingingStatus(cflow.Id);
                            writer.WriteString("Id", cflow.Id);
                            writer.WriteString("Url", cflow.Url);
                            break;

                        case "check":
                        case "check-bot":
                            PingResponse response = queryParams.ContainsKey("id")
                                ? Program.TwitchApi.ThirdParty.AuthorizationFlow.PingStatus(queryParams["id"])
                                : Program.TwitchApi.ThirdParty.AuthorizationFlow.PingStatus();

                            writer.WriteString("Id", response.Id);

                            if (response.Success)
                            {
                                if (queryParams["type"] == "check-bot")
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

                if (queryParams.ContainsKey("username") && !string.IsNullOrWhiteSpace(queryParams["username"]))
                {
                    GetUsersResponse response = await Program.TwitchApi.Helix.Users.GetUsersAsync(null, new List<string> { queryParams["username"] }).ConfigureAwait(false);

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
                sContent = await HandlePost(request, sContent).ConfigureAwait(false);
            }

            sContent = await UpdateFormInputsAsync(sContent).ConfigureAwait(false);

            byte[] content = Encoding.UTF8.GetBytes(sContent);
            Dictionary<string, List<string>> headers = new Dictionary<string, List<string>>
            {
                { "Content-Length", new List<string> { content.Length.ToString(CultureInfo.InvariantCulture) } },
                { "Content-Type", new List<string> { contentType } }
            };

            await HttpServer.SendHTTPResponseAsync(request.TcpClient, request.Stream, HttpStatusCode.OK, content, headers).ConfigureAwait(false);
        }

        private static async Task<string> HandlePost(HttpServerRequestMessage request, string sContent)
        {
            Dictionary<string, string> postParams = new Dictionary<string, string>();

            foreach (string s in (await request.Content.ReadAsStringAsync().ConfigureAwait(false)).Split('&'))
            {
                string[] post = s.Split('=', 2);

                if (!string.IsNullOrWhiteSpace(post[0]) && !string.IsNullOrWhiteSpace(WebUtility.UrlDecode(post[1])))
                {
                    postParams.Add(post[0].ToLowerInvariant(), WebUtility.UrlDecode(post[1]));
                }
            }

            //TODO: Update Settings

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
                IMongoCollection<UserDocument> collection = DatabaseClient.Instance.MongoDatabase.GetCollection<UserDocument>("users");
                IMongoCollection<AuthenticationDocument> collection2 = DatabaseClient.Instance.MongoDatabase.GetCollection<AuthenticationDocument>("authentications");

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

        #endregion Private Methods
    }
}