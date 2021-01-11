using System.Text.Json.Serialization;

namespace Negri.Twitch.Api
{
    public class Pagination
    {
        [JsonPropertyName("cursor")]
        public string Cursor { get; set; }
    }
}