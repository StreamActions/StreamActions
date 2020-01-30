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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace StreamActions.Http
{
    /// <summary>
    /// An HTTP Server. Currently only supports Authorization headers and WwebSocket Upgrade.
    /// </summary>
    internal class HttpServer : IDisposable
    {
        #region Public Methods

        public void Dispose() => this.Dispose(true);

        #endregion Public Methods

        #region Internal Constructors

        internal HttpServer(string listenIp, int listenPort, bool useSsl, string certFile = null)
        {
            IPAddress ipAddress = listenIp == "*" ? IPAddress.Any : listenIp == "127.0.0.1" ? IPAddress.Loopback : IPAddress.Parse(listenIp);
            this._server = new TcpListener(ipAddress, listenPort);
            this._useSsl = useSsl;

            if (this._useSsl)
            {
                this._certificate = new X509Certificate2();
                this._certificate.Import(certFile);
            }
        }

        #endregion Internal Constructors

        #region Internal Methods

        internal static bool IsWebSocketUpgradeRequest(HttpServerRequestMessage request) => request.Headers.Contains("Upgrade") && request.Headers.GetValues("Upgrade").Contains("websocket")
            && request.Headers.Contains("Connection") && request.Headers.GetValues("Connection").Contains("Upgrade");

        internal static async Task SendHTTPResponseAsync(TcpClient client, Stream stream, HttpStatusCode statusCode, byte[] content, Dictionary<string, List<string>> httpHeaders = null, bool isClosing = true)
        {
            if (httpHeaders is null)
            {
                httpHeaders = new Dictionary<string, List<string>>();
            }

            if (!httpHeaders.ContainsKey("Content-Length"))
            {
                httpHeaders.Add("Content-Length", new List<string> { content.Length.ToString(CultureInfo.InvariantCulture) });
            }

            if (!httpHeaders.ContainsKey("Content-Type") && content.Length > 0)
            {
                httpHeaders.Add("Content-Type", new List<string> { "text/plain" });
            }

            if (!httpHeaders.ContainsKey("Last-Modified"))
            {
                httpHeaders.Add("Last-Modified", new List<string> { DateTime.UtcNow.ToString("R", CultureInfo.InvariantCulture) });
            }

            _ = httpHeaders.Remove("Date");
            httpHeaders.Add("Date", new List<string> { DateTime.UtcNow.ToString("R", CultureInfo.InvariantCulture) });

            if (!httpHeaders.ContainsKey("Server"))
            {
                httpHeaders.Add("Server", new List<string> { typeof(Program).Assembly.GetName().FullName + "/" + typeof(Program).Assembly.GetName().Version.ToString() });
            }

            if (isClosing)
            {
                httpHeaders.Add("Connection", new List<string> { "close" });
            }

            string header = "HTTP/1.1 " + (int)statusCode + " " + GetStatusDescription(statusCode) + _endl;

            foreach (KeyValuePair<string, List<string>> kvp in httpHeaders)
            {
                foreach (string value in kvp.Value)
                {
                    header += kvp.Key + ": " + value + _endl;
                }
            }

            header += _endl;

            await stream.WriteAsync(Encoding.UTF8.GetBytes(header));

            if (content.Length > 0)
            {
                await stream.WriteAsync(content);
            }

            if (isClosing)
            {
                stream.Close();
                client.Close();
            }
        }

        internal void Start() => this._server.Start();

        internal void Stop() => this._server.Stop();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "As designed by Microsoft")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Closed by SendHTTPResponseAsync")]
        internal async Task<HttpServerRequestMessage> WaitForRequestAsync()
        {
            TcpClient client = await this._server.AcceptTcpClientAsync().ConfigureAwait(false);
            client.ReceiveTimeout = _requestTimeout;
            Stream stream = client.GetStream();

            if (this._useSsl)
            {
                SslStream sslStream = new SslStream(stream, false);
                try
                {
                    await sslStream.AuthenticateAsServerAsync(this._certificate, clientCertificateRequired: false, checkCertificateRevocation: true).ConfigureAwait(false);
                }
                catch (AuthenticationException)
                {
                    stream.WriteTimeout = _streamTimeout;
                    await SendHTTPResponseAsync(client, sslStream, HttpStatusCode.InternalServerError, Array.Empty<byte>()).ConfigureAwait(false);
                    return null;
                }

                stream = sslStream;
            }

            stream.ReadTimeout = _streamTimeout;
            stream.WriteTimeout = _streamTimeout;

            return await WaitAndParseRequestAsync(client, stream).ConfigureAwait(false);
        }

        #endregion Internal Methods

        #region Protected Methods

        protected virtual void Dispose(bool disposing)
        {
            if (!this._disposedValue)
            {
                if (disposing)
                {
                    this.Stop();
                    this._certificate.Dispose();
                }

                this._disposedValue = true;
            }
        }

        #endregion Protected Methods

        #region Private Fields

        private const string _endh = _endl + _endl;
        private const string _endl = "\r\n";
        private const int _requestTimeout = 15000;
        private const int _streamTimeout = 5000;
        private static readonly Regex _headerRegex = new Regex(@"^(?<field>\S*): (?<value>[\S\s]+)$", RegexOptions.Compiled);
        private static readonly Regex _requestRegex = new Regex(@"^(?<method>(GET|HEAD|POST|PUT|DELETE|PATCH)) (?<path>\/\S*) (?<protocol>HTTP\/1\.1)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private readonly X509Certificate2 _certificate;
        private readonly TcpListener _server;
        private readonly bool _useSsl;
        private bool _disposedValue = false;

        #endregion Private Fields

        #region Private Methods

        private static HttpMethod GetHttpMethod(string method) => method switch
        {
            "GET" => HttpMethod.Get,
            "HEAD" => HttpMethod.Head,
            "POST" => HttpMethod.Post,
            "PUT" => HttpMethod.Put,
            "DELETE" => HttpMethod.Delete,
            "PATCH" => HttpMethod.Patch,

            _ => null,
        };

        private static string GetStatusDescription(HttpStatusCode code) =>
                    GetStatusDescription((int)code);

        private static string GetStatusDescription(int code) =>
            code switch
            {
                100 => "Continue",
                101 => "Switching Protocols",
                102 => "Processing",
                103 => "Early Hints",

                200 => "OK",
                201 => "Created",
                202 => "Accepted",
                203 => "Non-Authoritative Information",
                204 => "No Content",
                205 => "Reset Content",
                206 => "Partial Content",
                207 => "Multi-Status",
                208 => "Already Reported",
                226 => "IM Used",

                300 => "Multiple Choices",
                301 => "Moved Permanently",
                302 => "Found",
                303 => "See Other",
                304 => "Not Modified",
                305 => "Use Proxy",
                307 => "Temporary Redirect",
                308 => "Permanent Redirect",

                400 => "Bad Request",
                401 => "Unauthorized",
                402 => "Payment Required",
                403 => "Forbidden",
                404 => "Not Found",
                405 => "Method Not Allowed",
                406 => "Not Acceptable",
                407 => "Proxy Authentication Required",
                408 => "Request Timeout",
                409 => "Conflict",
                410 => "Gone",
                411 => "Length Required",
                412 => "Precondition Failed",
                413 => "Request Entity Too Large",
                414 => "Request-Uri Too Long",
                415 => "Unsupported Media Type",
                416 => "Requested Range Not Satisfiable",
                417 => "Expectation Failed",
                421 => "Misdirected Request",
                422 => "Unprocessable Entity",
                423 => "Locked",
                424 => "Failed Dependency",
                426 => "Upgrade Required", // RFC 2817
                428 => "Precondition Required",
                429 => "Too Many Requests",
                431 => "Request Header Fields Too Large",
                451 => "Unavailable For Legal Reasons",

                500 => "Internal Server Error",
                501 => "Not Implemented",
                502 => "Bad Gateway",
                503 => "Service Unavailable",
                504 => "Gateway Timeout",
                505 => "Http Version Not Supported",
                506 => "Variant Also Negotiates",
                507 => "Insufficient Storage",
                508 => "Loop Detected",
                510 => "Not Extended",
                511 => "Network Authentication Required",

                _ => null,
            };

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Upstream type is responsible for this")]
        private static async Task<HttpServerRequestMessage> WaitAndParseRequestAsync(TcpClient client, Stream stream)
        {
            byte[] buffer = new byte[64];
            StringBuilder requestData = new StringBuilder();
            Decoder decoder = Encoding.UTF8.GetDecoder();

            int bytes;
            do
            {
                bytes = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);

                char[] chars = new char[decoder.GetCharCount(buffer, 0, bytes)];
                _ = decoder.GetChars(buffer, 0, bytes, chars, 0);
                _ = requestData.Append(chars);

                if (requestData.ToString().IndexOf(_endh, StringComparison.InvariantCulture) != -1)
                {
                    break;
                }
            } while (bytes != 0);

            string requestString = requestData.ToString();
            List<string> lines = requestString.Split(_endl).ToList();
            string requestLine = lines[0];
            lines.RemoveAt(0);

            if (!_requestRegex.IsMatch(requestLine))
            {
                await SendHTTPResponseAsync(client, stream, HttpStatusCode.BadRequest, Array.Empty<byte>()).ConfigureAwait(false);
                return null;
            }

            Match match = _requestRegex.Match(requestLine);

            HttpServerRequestMessage requestMessage = new HttpServerRequestMessage(GetHttpMethod(match.Groups["method"].Value.ToUpperInvariant()), match.Groups["path"].Value)
            {
                Version = HttpVersion.Version11,
                TcpClient = client,
                Stream = stream
            };

            foreach (string line in lines)
            {
                if (string.IsNullOrEmpty(line))
                {
                    break;
                }

                if (!_headerRegex.IsMatch(line))
                {
                    await SendHTTPResponseAsync(client, stream, HttpStatusCode.BadRequest, Array.Empty<byte>()).ConfigureAwait(false);
                    requestMessage.Dispose();
                    return null;
                }

                match = _headerRegex.Match(line);

                requestMessage.Headers.Add(match.Groups["field"].Value, match.Groups["value"].Value.Trim());
            }

            if (requestMessage.Headers.Host is null)
            {
                await SendHTTPResponseAsync(client, stream, HttpStatusCode.BadRequest, Array.Empty<byte>()).ConfigureAwait(false);
                requestMessage.Dispose();
                return null;
            }

            if (requestMessage.Headers.Contains("Content-Length") && int.TryParse(requestMessage.Headers.GetValues("Content-Length").First(), out int contentLength))
            {
                requestData = new StringBuilder(requestString.Substring(requestString.IndexOf(_endh, StringComparison.InvariantCultureIgnoreCase) + _endh.Length));

                do
                {
                    bytes = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);

                    char[] chars = new char[decoder.GetCharCount(buffer, 0, bytes)];
                    _ = decoder.GetChars(buffer, 0, bytes, chars, 0);
                    _ = requestData.Append(chars);

                    if (requestData.ToString().Length >= contentLength)
                    {
                        break;
                    }
                } while (bytes != 0);

                requestString = requestData.ToString();

                if (requestString.Length > contentLength)
                {
                    requestString = requestString.Substring(0, contentLength);
                }

                requestMessage.Content = new StringContent(requestString);
            }

            return requestMessage;
        }

        #endregion Private Methods
    }
}