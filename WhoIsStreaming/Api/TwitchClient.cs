using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Negri.Twitch.Api
{
    public class TwitchClient
    {
        private readonly string _clientId;
        private readonly string _clientSecret;

        private readonly HttpClient _client = new() {BaseAddress = new Uri("https://api.twitch.tv/helix/")};

        public TwitchClient(string clientId, string clientSecret)
        {
            _clientId = clientId;
            _clientSecret = clientSecret;
        }

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
            var results = new List<Game>();

            var s = Get($"search/categories?query={game}&first=100");
            do
            {
                var r = JsonSerializer.Deserialize<SearchGameResponse>(s);
                if (r?.Data != null)
                {
                    results.AddRange(r.Data);
                }

                if (!string.IsNullOrWhiteSpace(r?.Pagination?.Cursor))
                {
                    s = Get($"search/categories?query={game}&first=100&after={r.Pagination.Cursor}");
                }
                else
                {
                    break;
                }
            } while (true);

            return results;
        }

        public Game GetGame(string id)
        {
            var s = Get($"games?id={id}");
            var r = JsonSerializer.Deserialize<SearchGameResponse>(s);
            return r?.Data[0];
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

        private byte[] GetBytes(string url, string referrer = null)
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

                    var s = res.Content.ReadAsByteArrayAsync().Result;
                    return s;
                });


            return ss;
        }

        public void Dispose()
        {
            _client?.Dispose();
        }

        public IEnumerable<Stream> GetStreams(string id)
        {
            var results = new List<Stream>();

            var s = Get($"streams?game_id={id}&first=100");
            do
            {
                var r = JsonSerializer.Deserialize<GetStreamsResponse>(s);
                if (r?.Data != null)
                {
                    results.AddRange(r.Data);
                }

                if (!string.IsNullOrWhiteSpace(r?.Pagination?.Cursor))
                {
                    s = Get($"streams?game_id={id}&first=100&after={r.Pagination.Cursor}");
                }
                else
                {
                    break;
                }
            } while (true);

            var hash = new HashSet<string>();

            var finalList = new List<Stream>(results.Count);
            foreach (var st in results.OrderByDescending(st => st.ViewerCount))
            {
                if (!hash.Contains(st.UserId))
                {
                    finalList.Add(st);
                    hash.Add(st.UserId);
                }
            }

            return finalList;
        }

        public void DownloadFile(string url, string file, int width, int height)
        {
            url = url.Replace("{width}", width.ToString()).Replace("{height}", height.ToString());
            File.WriteAllBytes(file, GetBytes(url));
        }
    }
}