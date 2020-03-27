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

                if (!string.IsNullOrWhiteSpace(query[0]) && !string.IsNullOrWhiteSpace(query[1]))
                {
                    queryParams.Add(query[0].ToLowerInvariant(), query[1]);
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
                            CreatedFlow bflow = Program.TwitchApi.ThirdParty.AuthorizationFlow.CreateFlow("StreamActions-Bot (Bot/Chat_Login)", _chatAuthScopes);
                            Program.TwitchApi.ThirdParty.AuthorizationFlow.BeginPingingStatus(bflow.Id);
                            writer.WriteString("Id", bflow.Id);
                            writer.WriteString("Url", bflow.Url);
                            break;

                        case "single":
                            CreatedFlow sflow = Program.TwitchApi.ThirdParty.AuthorizationFlow.CreateFlow("StreamActions-Bot (Bot/All_Scopes)", _chatAuthScopes.Concat(_broadcasterAuthScopes));
                            Program.TwitchApi.ThirdParty.AuthorizationFlow.BeginPingingStatus(sflow.Id);
                            writer.WriteString("Id", sflow.Id);
                            writer.WriteString("Url", sflow.Url);
                            break;

                        case "broadcaster":
                            CreatedFlow cflow = Program.TwitchApi.ThirdParty.AuthorizationFlow.CreateFlow("StreamActions-Bot (Broadcaster)", _broadcasterAuthScopes.Concat(_broadcasterAuthScopes));
                            Program.TwitchApi.ThirdParty.AuthorizationFlow.BeginPingingStatus(cflow.Id);
                            writer.WriteString("Id", cflow.Id);
                            writer.WriteString("Url", cflow.Url);
                            break;

                        case "check":
                            PingResponse response = queryParams.ContainsKey("id")
                                ? Program.TwitchApi.ThirdParty.AuthorizationFlow.PingStatus(queryParams["id"])
                                : Program.TwitchApi.ThirdParty.AuthorizationFlow.PingStatus();

                            writer.WriteString("Id", response.Id);

                            if (response.Success)
                            {
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

            if (request.Method == HttpMethod.Put)
            {
                sContent = await HandlePut(request, sContent).ConfigureAwait(false);
            }

            byte[] content = Encoding.UTF8.GetBytes(sContent);
            Dictionary<string, List<string>> headers = new Dictionary<string, List<string>>
            {
                { "Content-Length", new List<string> { content.Length.ToString(CultureInfo.InvariantCulture) } },
                { "Content-Type", new List<string> { contentType } }
            };

            await HttpServer.SendHTTPResponseAsync(request.TcpClient, request.Stream, HttpStatusCode.OK, content, headers).ConfigureAwait(false);
        }

        private static async Task<string> HandlePut(HttpServerRequestMessage request, string sContent)
        {
        }

        #endregion Private Methods
    }
}