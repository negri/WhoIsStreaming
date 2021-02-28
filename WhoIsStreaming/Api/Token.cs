using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Negri.Twitch.Api
{
    [PublicAPI]
    public class Token
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}