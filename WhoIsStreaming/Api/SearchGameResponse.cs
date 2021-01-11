using System.Text.Json.Serialization;

namespace Negri.Twitch.Api
{
    public class SearchGameResponse : ResponseBase
    {
        [JsonPropertyName("data")]
        public Game[] Data { get; set; }
    }
}