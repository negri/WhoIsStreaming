using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Exceptions;

namespace Negri.Twitch.Commands
{
    [Command(nameof(Report), Description = "Collect who is streaming a given game at this moment.")]
    public class Report : ICommand
    {
        private readonly AppSettings _appSettings;

        public Report(AppSettings appSettings)
        {
            _appSettings = appSettings;
        }

        [CommandParameter(0, Name = "game", Description = "Game id to report.")]
        public string Game { get; set; }

        [CommandParameter(1, Name = "report", Description = "Path and name to the Excel report file.")]
        public string ReportFile { get; set; }

        [CommandOption("data-dir", 'd', Description = "The directory where to write collected data.", EnvironmentVariableName = "who-is-streaming-data-dir", IsRequired = true)]
        public string DataDir { get; set; } = string.Empty;

        [CommandOption("start", Description = "The start moment to report.")]
        public DateTime? Start { get; set; }

        [CommandOption("end", Description = "The end moment to stop reporting.")]
        public DateTime? End { get; set; }

        [CommandOption("period", 'p', Description = "The most recent period to report.")]
        public ReportPeriod? Period { get; set; }

        [CommandOption("use-utc", 'u', Description = "All input dates are in UTC.")]
        public bool UseUtc { get; set; } = false;

        public ValueTask ExecuteAsync(IConsole console)
        {
            FixDates();
            Debug.Assert(Start != null && End != null);

            if (!Directory.Exists(DataDir))
            {
                throw new CommandException($"The data directory '{DataDir}' does not exists.", (int)ReturnCode.DataDirectoryDoesNotExists);
            }

            var client = new Api.TwitchClient(_appSettings.ClientId, _appSettings.ClientSecret);
            client.Logon();

            // Check the game
            var game = client.GetGame(Game);
            if (game == null)
            {
                throw new CommandException($"Can't find a game with id '{Game}'.", (int) ReturnCode.GameNotFound);
            }

            console.Output.WriteLine(UseUtc
                ? $"Generating reports for streams from {Start} to {End} in game {game.Name}..."
                : $"Generating reports for streams from {Start.Value.ToLocalTime()} to {End.Value.ToLocalTime()} in game {game.Name}...");


            var di = new DirectoryInfo(DataDir);
            var mask = $"WIS.{Game}.????-??-??.??????.csv";
            var observations = new List<Observation>();
            foreach (var fi in di.EnumerateFiles(mask, SearchOption.TopDirectoryOnly))
            {
                observations.AddRange(ReadFile(fi.FullName));
            }
            
            return default;
        }

        private IEnumerable<Observation> ReadFile(string file)
        {
            var lines = File.ReadAllLines(file, encoding: Encoding.UTF8);
            if (lines.Length <= 1)
            {
                yield break;
            }

            for (var i = 1; i < lines.Length; ++i)
            {
                
            }
        }

        private void FixDates()
        {
            if (Start != null && End != null && Period != null)
            {
                throw new CommandException("Do not specify a period together a start AND end date.", (int) ReturnCode.ThreeDateParameters);
            }

            if (Start == null && End == null && Period == null)
            {
                throw new CommandException("At least one date parameter must be specified.", (int) ReturnCode.MissingDateParameters);
            }

            // Convert to UTC if inputted on local time
            if (!UseUtc)
            {
                Start = Start?.ToUniversalTime();
                End = End?.ToUniversalTime();
            }

            if (Start == null && End != null && Period == null)
            {
                throw new CommandException("If you set an end date then you must set a start date or a period.", (int) ReturnCode.MissingDateParameters);
            }

            if (Start != null && End != null && Start >= End)
            {
                throw new CommandException("The end moment must be after the start moment!", (int) ReturnCode.InvalidDateParameters);
            }

            if (Start == null && End == null && Period != null)
            {
                End = DateTime.UtcNow;
                switch (Period)
                {
                    case ReportPeriod.Day:
                        Start = End.Value.AddDays(-1);
                        break;
                    case ReportPeriod.Week:
                        Start = End.Value.AddDays(-7);
                        break;
                    case ReportPeriod.Month:
                        Start = End.Value.AddMonths(-1);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(Period), Period, "Invalid Period!");
                }
            }
            else if (Start != null && End == null && Period != null)
            {
                switch (Period)
                {
                    case ReportPeriod.Day:
                        End = Start.Value.AddDays(1);
                        break;
                    case ReportPeriod.Week:
                        End = Start.Value.AddDays(7);
                        break;
                    case ReportPeriod.Month:
                        End = Start.Value.AddMonths(1);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(Period), Period, "Invalid Period!");
                }
            }
            else if (Start == null && End != null && Period != null)
            {
                switch (Period)
                {
                    case ReportPeriod.Day:
                        Start = End.Value.AddDays(-1);
                        break;
                    case ReportPeriod.Week:
                        Start = End.Value.AddDays(-7);
                        break;
                    case ReportPeriod.Month:
                        Start = End.Value.AddMonths(-1);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(Period), Period, "Invalid Period!");
                }
            }
            else if (Start != null && End == null && Period == null)
            {
                End = DateTime.UtcNow;
            }
            else
            {
                throw new InvalidOperationException("Well... sorry!");
            }
        }


        public record Observation(long UserId, string UserName, string Language, int Viewers, DateTime Start, int Minutes, string Title);
    }

    
    
}