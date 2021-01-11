using System.Text.Json.Serialization;

namespace Negri.Twitch.Api
{
    public abstract class ResponseBase
    {
        [JsonPropertyName("pagination")]
        public Pagination Pagination { get; set; }
    }
}