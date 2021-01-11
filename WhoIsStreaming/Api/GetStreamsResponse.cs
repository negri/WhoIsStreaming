using System.Text.Json.Serialization;

namespace Negri.Twitch.Api
{
    public class GetStreamsResponse : ResponseBase
    {
        [JsonPropertyName("data")]
        public Stream[] Data { get; set; }
    }
}