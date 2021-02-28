using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Negri.Twitch.Api
{
    [PublicAPI]
    public class Pagination
    {
        [JsonPropertyName("cursor")]
        public string Cursor { get; set; }
    }
}