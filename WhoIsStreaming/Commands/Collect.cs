﻿using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Exceptions;

namespace Negri.Twitch.Commands
{
    [Command("Collect", Description = "Collect who is streaming a given game at this moment.")]
    public class Collect : ICommand
    {
        private AppSettings _appSettings;

        public Collect(AppSettings appSettings)
        {
            _appSettings = appSettings;
        }

        [CommandParameter(0, Name = "game", Description = "Game id to retrieve streamers.")]
        public string Game { get; set; }

        [CommandOption("DataDir", 'd', Description = "The directory where to write collected data.", EnvironmentVariableName = "WhoIsStreamingDataDir", IsRequired = true)]
        public string DataDir { get; set; }

        [CommandOption("SaveThumbnails", 't', Description = "If thumbnails should be saved.")]
        public bool SaveThumbnails { get; set; } = false;

        [CommandOption("MinViewers", 'v', Description = "Minimum numbers of viewers to save.")]
        public int MinViewers { get; set; } = 0;

        public ValueTask ExecuteAsync(IConsole console)
        {
            var client = new Api.TwitchClient(_appSettings.ClientId, _appSettings.ClientSecret);
            client.Logon();

            if (!Directory.Exists(DataDir))
            {
                throw new CommandException($"The data directory '{DataDir}' does not exists.", (int)ReturnCode.DataDirectoryDoesNotExists);
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
                throw new CommandException($"The data directory '{DataDir}' is not writable.", (int)ReturnCode.DataDirectoryNotWritable);
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



            return default;
        }
    }
}