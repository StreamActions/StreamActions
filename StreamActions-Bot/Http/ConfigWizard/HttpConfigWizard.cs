using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

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

        #region Private Methods

        private static async Task HandleGet(HttpServerRequestMessage request)
        {
            string resource = "ConfigWizard.html";
            string contentType = "text/html";

            if (request.RequestUri.AbsolutePath.EndsWith("TwitchConnect.png", StringComparison.OrdinalIgnoreCase))
            {
                resource = "TwitchConnect.png";
                contentType = "image/png";
            }

            using StreamReader reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("StreamActions.Http.ConfigWizard." + resource));
            string sContent = await reader.ReadToEndAsync().ConfigureAwait(false);

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