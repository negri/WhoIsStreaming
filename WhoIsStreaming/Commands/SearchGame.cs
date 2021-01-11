using System.Linq;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Exceptions;

namespace Negri.Twitch.Commands
{
    [Command("SearchGame", Description = "Search a game on Twitch.")]
    public class SearchGame : ICommand
    {
        private readonly AppSettings _appSettings;

        public SearchGame(AppSettings appSettings)
        {
            _appSettings = appSettings;
        }

        [CommandParameter(0, Name = "game", Description = "part of the name of the game to search for.")]
        public string GameName { get; set; }

        public ValueTask ExecuteAsync(IConsole console)
        {
            var client = new Api.TwitchClient(_appSettings.ClientId, _appSettings.ClientSecret);
            client.Logon();
            var games = client.SearchGame(GameName).ToArray();

            console.Output.WriteLine($"Search for '{GameName}' returned {games.Length} results:");
            console.Output.WriteLine("Id              Game");
            foreach (var g in games)
            {
                console.Output.WriteLine($"{g.Id.PadRight(15)} {g.Name}");
            }


            return default;
        }
    }

    public enum ReturnCode
    {
        NotImplemented = 1,
    }
}