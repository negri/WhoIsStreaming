using System.Text.Json.Serialization;

namespace Negri.Twitch.Api
{

    public abstract class ResponseBase
    {
        [JsonPropertyName("pagination")]
        public Pagination Pagination { get; set; }
    }

    public class Pagination
    {
        [JsonPropertyName("cursor")]
        public string Cursor { get; set; }
    }

    public class SearchGameResponse : ResponseBase
    {
        [JsonPropertyName("data")]
        public Game[] Data { get; set; }
    }

    public class Token
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    public class Game
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }
    }
}