using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Negri.Twitch.Api
{
    public class TwitchClient
    {
        private readonly string _clientId;
        private readonly string _clientSecret;
        
        public TwitchClient(string clientId, string clientSecret)
        {
            _clientId = clientId;
            _clientSecret = clientSecret;
        }

        private HttpClient _client = new HttpClient { BaseAddress = new Uri("https://api.twitch.tv/helix/") };

        public void Logon()
        {
            var url = $"https://id.twitch.tv/oauth2/token?client_id={_clientId}&client_secret={_clientSecret}&grant_type=client_credentials";
            var s = Post(url, null);

            
            var r = JsonSerializer.Deserialize<Token>(s);
            if (r == null)
            {
                throw new InvalidOperationException("The access token could not be retrieved.");
            }

            _client.DefaultRequestHeaders.Accept.Clear();
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            _client.DefaultRequestHeaders.UserAgent.Clear();

            _client.DefaultRequestHeaders.Add("User-Agent", "WhoIsStreaming by JP Negri Coder");

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", r.AccessToken);
            _client.DefaultRequestHeaders.Add("Client-Id", _clientId);
        }

        public IEnumerable<Game> SearchGame(string game)
        {
            var s = Get($"search/categories?query={game}");
            
            var r = JsonSerializer.Deserialize<SearchGameResponse>(s);
            if (r == null)
            {
                return Enumerable.Empty<Game>();
            }

            return r.Data ?? Enumerable.Empty<Game>();
        }

        private string Post(string url, object post, string referrer = null)
        {
            
            var ss = WebApiRetryPolicy.ExecuteAction(() =>
            {
                var req = new HttpRequestMessage(HttpMethod.Post, url);
                if (!string.IsNullOrWhiteSpace(referrer))
                {
                    req.Headers.Referrer = new Uri(referrer);
                }

                if (post != null)
                {
                    req.Content = new JsonContent(post);
                }

                var res = _client.SendAsync(req).Result;
                if (!res.IsSuccessStatusCode)
                {
                    throw new WebApiException(url, res.StatusCode, res.ReasonPhrase);
                }

                var s = res.Content.ReadAsStringAsync().Result;
                return s;
            });

            
            return ss;
        }

        private string Get(string url, string referrer = null)
        {
            
            var ss =
                WebApiRetryPolicy.ExecuteAction(() =>
                {
                    var req = new HttpRequestMessage(HttpMethod.Get, url);
                    if (!string.IsNullOrWhiteSpace(referrer))
                    {
                        req.Headers.Referrer = new Uri(referrer);
                    }

                    var res = _client.SendAsync(req).Result;
                    if (!res.IsSuccessStatusCode)
                    {
                        throw new WebApiException(url, res.StatusCode, res.ReasonPhrase);
                    }

                    var s = res.Content.ReadAsStringAsync().Result;
                    return s;
                });

            
            return ss;
        }

        public void Dispose()
        {
            _client?.Dispose();
        }

    }
}
