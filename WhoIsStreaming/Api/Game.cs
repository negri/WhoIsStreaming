using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Negri.Twitch.Api
{
    [PublicAPI]
    public class Game
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }
    }
}