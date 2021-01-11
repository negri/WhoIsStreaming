using System;
using System.Net;

namespace Negri.Twitch.Api
{
    /// <summary>
    /// Exceção quando uma API Web falhar por erros na chamada
    /// </summary>
    public class WebApiException : ApplicationException
    {
        public string Url { get; }
        public HttpStatusCode StatusCode { get; }
        public string ReasonPhrase { get; }

        public WebApiException(string url, HttpStatusCode statusCode, string reasonPhrase) : base($"HTTP {statusCode}: {reasonPhrase} at {url}")
        {
            Url = url;
            StatusCode = statusCode;
            ReasonPhrase = reasonPhrase;
        }
    }
}