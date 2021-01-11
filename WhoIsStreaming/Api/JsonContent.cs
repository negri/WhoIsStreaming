using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Negri.Twitch.Api
{
    public class JsonContent : StringContent
    {
        public JsonContent(object obj) :
            base(JsonSerializer.Serialize(obj), Encoding.UTF8, "application/json")
        { }
    }
}