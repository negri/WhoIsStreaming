using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Exceptions;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;

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

        [CommandOption("excel-file", 'e', Description = "The Excel report to write.")]
        public string ExcelFile { get; set; }

        [CommandOption("data-dir", 'd', Description = "The directory where to read collected data.", EnvironmentVariableName = "who-is-streaming-data-dir", IsRequired = true)]
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
                Debug.Assert(Start != null, nameof(Start) + " != null");
                Debug.Assert(End != null, nameof(End) + " != null");

                observations.AddRange(ReadFile(fi.FullName, Start.Value, End.Value));
            }

            console.Output.WriteLine($"{observations.Count:N0} observations found, building sessions...");
            if (observations.Count <= 0)
            {
                throw new CommandException("No streams where found.", (int)ReturnCode.NoObservations);
            }

            var sessions = GetSessions(observations.OrderBy(o => o.Moment)).OrderByDescending(s => s.Observations).ToList();
            console.Output.WriteLine($"{sessions.Count:N0} sessions found for a total of {sessions.Sum(s => s.Duration.TotalHours):N0} hours.");

            console.Output.WriteLine($"Streamer                       Language Avg Viewers Max Viewers Duration");
            foreach (var s in sessions)
            {
                console.Output.WriteLine($"{s.UserName,-30} {s.Language,-8} {s.AverageViewers,11:N0} {s.MaxViewers,11:N0} {s.Duration,8:c}");
            }

            if (!string.IsNullOrWhiteSpace(ExcelFile))
            {
                WriteExcel(ExcelFile, observations, sessions);
            }

            return default;
        }

        private void WriteExcel(string file, List<Observation> observations, List<Session> sessions)
        {
           
        }

        private static IEnumerable<Session> GetSessions(IEnumerable<Observation> observations)
        {
            var sessions = new Dictionary<(long userId, DateTime start), Session>();

            foreach (var o in observations)
            {
                var k = (o.UserId, o.Start);
                if (!sessions.TryGetValue(k, out var session))
                {
                    session = new Session
                    {
                        UserId = o.UserId,
                        UserName = o.UserName,
                        Language = o.Language,
                        Start = o.Start

                    };
                    sessions.Add(k, session);
                }

                session.Add(o);
            }

            return sessions.Values;
        }

        private static IEnumerable<Observation> ReadFile(string file, DateTime start, DateTime end)
        {
            using var reader = new StreamReader(file);

            using var csv = new CsvReader(reader,
                new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    IgnoreBlankLines = true,
                    BadDataFound = (e => { return; })
                });

            var records = csv.GetRecords<Observation>().Where(o => o.Moment > start && o.Moment <= end).ToList();
            return records;
        }

        private void FixDates()
        {
            if (Start != null && End != null && Period != null)
            {
                throw new CommandException("Do not specify a period together a start AND end date.", (int) ReturnCode.ThreeDateParameters);
            }

            if (Start == null && End == null && Period == null)
            {
                End = UseUtc ? DateTime.UtcNow : DateTime.Now;
                Period = ReportPeriod.Week;
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

            if (Start >= End)
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

        private class Session
        {
            public long UserId { get; init; }
            public string UserName { get; init; }
            public string Language { get; init; }
            public DateTime Start { get; init; }

            public string Title => _titles.OrderByDescending(kv => kv.Value).FirstOrDefault().Key;
            private readonly Dictionary<string, int> _titles = new Dictionary<string, int>(2);

            public DateTime End { get; set; }
            public int Observations { get; set; }
            public int AverageViewers => CumulativeViewers / Observations;
            public int MaxViewers { get; set; }
            public int CumulativeViewers { get; private set; }

            public TimeSpan Duration => End - Start;

            public void Add(Observation o)
            {
                // The streamer can change the title on the middle of the session. Let's report the most frequent title used
                if (_titles.TryGetValue(o.Title, out var freq))
                {
                    _titles[o.Title] = freq + 1;
                }
                else
                {
                    _titles[o.Title] = 1;
                }

                End = o.Moment;
                Observations++;
                if (o.Viewers > MaxViewers)
                {
                    MaxViewers = o.Viewers;
                }

                CumulativeViewers += o.Viewers;
            }
        }

        private record Observation
        {
            [Name("User Id")]
            public long UserId { get; init; }

            [Name("User Name")]
            public string UserName { get; init; } 

            public string Language { get; init; } 

            public int Viewers { get; init; }

            [Name("Started At")]
            public DateTime Start { get; init; }

            [Name("Running Minutes")]
            public int Minutes { get; init; }

            public string Title { get; init; }

            public DateTime Moment => Start.AddMinutes(Minutes);
        }
            
            
    }

    
    
}