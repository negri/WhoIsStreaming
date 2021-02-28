using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Negri.Twitch.Api
{
    [PublicAPI]
    public class SearchGameResponse : ResponseBase
    {
        [JsonPropertyName("data")]
        public Game[] Data { get; set; }
    }
}