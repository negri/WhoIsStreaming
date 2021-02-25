using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Exceptions;

namespace Negri.Twitch.Commands
{
    [Command(nameof(Collect), Description = "Collect who is streaming a given game at this moment.")]
    public class Collect : ICommand
    {
        private readonly AppSettings _appSettings;

        public Collect(AppSettings appSettings)
        {
            _appSettings = appSettings;
        }

        [CommandParameter(0, Name = "game", Description = "Game id to retrieve streamers.")]
        public string Game { get; set; }

        [CommandOption("data-dir", 'd', Description = "The directory where to write collected data.", EnvironmentVariableName = "who-is-streaming-data-dir")]
        public string DataDir { get; set; }

        [CommandOption("save-thumbs", 't', Description = "If thumbnails should be saved.")]
        public bool SaveThumbnails { get; set; } = false;

        [CommandOption("min-viewers", 'v', Description = "Minimum numbers of viewers to save.")]
        public int MinViewers { get; set; } = 0;

        [CommandOption("min-viewers-thumbs", Description = "Minimum numbers of viewers to save.")]
        public int MinViewersForThumbnails { get; set; } = 0;

        public ValueTask ExecuteAsync(IConsole console)
        {
            var client = new Api.TwitchClient(_appSettings.ClientId, _appSettings.ClientSecret);
            client.Logon();

            var saveFiles = false;
            if (!string.IsNullOrWhiteSpace(DataDir))
            {
                if (!Directory.Exists(DataDir))
                {
                    throw new CommandException($"The data directory '{DataDir}' does not exists.", (int) ReturnCode.DataDirectoryDoesNotExists);
                }

                // Is writable?
                var testFile = Path.Combine(DataDir, "WriteTestFile.txt");
                try
                {
                    File.WriteAllText(testFile, "Yup! It works!");
                    File.Delete(testFile);
                }
                catch (Exception ex)
                {
                    throw new CommandException($"The data directory '{DataDir}' is not writable: {ex.Message}", (int) ReturnCode.DataDirectoryNotWritable);
                }
                saveFiles = true;
            }

            // Check the game
            var game = client.GetGame(Game);
            if (game == null)
            {
                throw new CommandException($"Can't find a game with id '{Game}'.", (int)ReturnCode.GameNotFound);
            }

            console.Output.WriteLine($"Searching for live streamers on '{game.Name}'...");

            var streams = client.GetStreams(game.Id).Where(s => s.ViewerCount >= MinViewers).ToArray();

            console.Output.WriteLine($"Streamer                       Language Viewers");
            foreach (var s in streams)
            {
                console.Output.WriteLine($"{s.UserName,-30} {s.Language,-8} {s.ViewerCount,7:N0}");
            }

            if (!saveFiles)
            {
                return default;
            }

            var now = DateTime.UtcNow;
            if (SaveThumbnails)
            {
                var streamsToGetThumbs = streams.Where(s => s.ViewerCount >= MinViewersForThumbnails).ToArray();

                console.Output.WriteLine("Downloading thumbnails, it may take a while...");
                var thumbFolder = Path.Combine(DataDir, $"Thumb.{game.Id}.{now:yyyy-MM-dd.HHmmss}");
                Directory.CreateDirectory(thumbFolder);

                var count = 0;
                var messageLock = new object();
                
                Parallel.ForEach(streamsToGetThumbs, (s, ps, loopCount) =>
                {
                    Interlocked.Increment(ref count);
                    lock (messageLock)
                    {
                        console.Output.WriteLine($"Downloading thumbnail {count} out of {streamsToGetThumbs.Length}...");
                    }
                    var thumbFile = Path.Combine(thumbFolder, $"{s.ViewerCount:0000000}.{s.UserName}.jpg");
                    client.DownloadFile(s.ThumbnailUrl, thumbFile, 300, 200);
                    s.ThumbnailFile = thumbFile;
                });
                
            }

            var sb = new StringBuilder();

            sb.AppendLine("User Id,User Name,Language,Viewers,Started At,Running Minutes,Thumbnail File,Title");
            foreach (var s in streams)
            {
                sb.AppendLine($"\"{s.UserId}\",\"{s.UserName}\",\"{s.Language}\",{s.ViewerCount},{s.StartedAt:yyyy-MM-dd HH:mm:ss},{(now-s.StartedAt).TotalMinutes:0},\"{s.ThumbnailFile}\",\"{s.NormalizedTitle}\"");
            }

            var fileName = Path.Combine(DataDir, $"WIS.{game.Id}.{now:yyyy-MM-dd.HHmmss}.csv");
            File.WriteAllText(fileName, sb.ToString(), Encoding.UTF8);

            console.Output.WriteLine($"Data collected on file '{fileName}' for {streams.Length:N0} streamers.");


            return default;
        }
    }
}