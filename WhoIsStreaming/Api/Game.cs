using System.Text.Json.Serialization;

namespace Negri.Twitch.Api
{
    public class Game
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }
    }
}