using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Negri.Twitch.Api
{
    [PublicAPI]
    public class GetStreamsResponse : ResponseBase
    {
        [JsonPropertyName("data")]
        public Stream[] Data { get; set; }
    }
}