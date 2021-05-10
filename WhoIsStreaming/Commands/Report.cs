using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Exceptions;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using JetBrains.Annotations;
using Negri.Twitch.Api;
using OfficeOpenXml;

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

        [PublicAPI]
        [CommandParameter(0, Name = "game", Description = "Game id to report.")]
        public string Game { get; set; }

        [PublicAPI]
        [CommandOption("excel-file", 'e', Description = "The Excel report to write.")]
        public string ExcelFile { get; set; }

        [PublicAPI]
        [CommandOption("data-dir", 'd', Description = "The directory where to read collected data.", EnvironmentVariableName = "who-is-streaming-data-dir", IsRequired = true)]
        public string DataDir { get; set; } = string.Empty;

        [CommandOption("start", Description = "The start moment to report.")]
        public DateTime? Start { get; set; }

        [CommandOption("end", Description = "The end moment to stop reporting.")]
        public DateTime? End { get; set; }

        [CommandOption("period", 'p', Description = "The most recent period to report.")]
        public ReportPeriod? Period { get; set; }

        [PublicAPI]
        [CommandOption("use-utc", 'u', Description = "All input dates are in UTC.")]
        public bool UseUtc { get; set; }

        [PublicAPI]
        [CommandOption("verbose", Description = "Verbose output as the report runs.")]
        public bool Verbose { get; set; } = false;

        public ValueTask ExecuteAsync(IConsole console)
        {
            FixDates();

            Debug.Assert(Start != null && End != null);

            if (!Directory.Exists(DataDir))
            {
                throw new CommandException($"The data directory '{DataDir}' does not exists.", (int) ReturnCode.DataDirectoryDoesNotExists);
            }

            var client = new TwitchClient(_appSettings.ClientId, _appSettings.ClientSecret);
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

            Debug.Assert(Start != null, nameof(Start) + " != null");
            Debug.Assert(End != null, nameof(End) + " != null");
            var minFileMoment = Start.Value.AddHours(-1);
            var maxFileMoment = End.Value.AddHours(1);

            var di = new DirectoryInfo(DataDir);
            var mask = $"WIS.{Game}.????-??-??.??????.csv";
            var observations = new List<Observation>();
            foreach (var fi in di.EnumerateFiles(mask, SearchOption.TopDirectoryOnly))
            {
                var momentPart = fi.Name.Substring(fi.Name.Length - 21);
                momentPart = momentPart.Substring(0, 17);
                // ReSharper disable once StringLiteralTypo
                var moment = DateTime.ParseExact(momentPart, "yyyy-MM-dd.HHmmss", CultureInfo.InvariantCulture);

                if (minFileMoment <= moment && moment <= maxFileMoment)
                {
                    if (Verbose)
                    {
                        console.Output.WriteLine($"  reading file {fi.Name}...");
                    }
                    observations.AddRange(ReadFile(fi.FullName, Start.Value, End.Value));
                }
                
            }

            console.Output.WriteLine($"{observations.Count:N0} observations found, building sessions...");
            if (observations.Count <= 0)
            {
                throw new CommandException("No streams where found.", (int) ReturnCode.NoObservations);
            }

            var sessions = GetSessions(observations.OrderBy(o => o.Moment)).OrderByDescending(s => s.ViewersMinutes).ToList();
            console.Output.WriteLine($"{sessions.Count:N0} sessions found for a total of {sessions.Sum(s => s.Duration.TotalHours):N0} hours.");

            console.Output.WriteLine("Streamer                       Language Avg Viewers Max Viewers Duration");
            foreach (var s in sessions)
            {
                console.Output.WriteLine($"{s.UserName,-30} {s.Language,-8} {s.AverageViewers,11:N0} {s.MaxViewers,11:N0} {s.Duration,8:c}");
            }

            // Calculates The Hourly Average Viewers 
            var hourlyById = from g in observations.GroupBy(o => (hour: o.MomentHour, userId: o.UserId))
                let avgViewers = g.Average(o => o.Viewers)
                select (g.Key.hour, g.Key.userId, avgViewers);

            var hourly = from g in hourlyById.GroupBy(hid => hid.hour)
                let streamersCount = g.Count()
                let avgViewers = g.Sum(hid => hid.avgViewers)
                select new Hourly(g.Key, streamersCount, (int)avgViewers);

            if (!string.IsNullOrWhiteSpace(ExcelFile))
            {
                console.Output.WriteLine($"Creating Excel report at {ExcelFile}...");
                WriteExcel(game.Name, observations, sessions, hourly);
                console.Output.WriteLine($"Excel report saved on '{ExcelFile}'.");
            }

            return default;
        }

        private void WriteExcel(string gameName, IEnumerable<Observation> observations, IEnumerable<Session> sessions, IEnumerable<Hourly> viewership)
        {
            var templateFile = Path.Combine(AppContext.BaseDirectory, "Template.xlsx");

            var templateFileInfo = new FileInfo(templateFile);
            using var package = new ExcelPackage(templateFileInfo);

            // The sessions
            var sessionsSheet = package.Workbook.Worksheets["Sessions"];
            sessionsSheet.Cells[2, 1].Value = gameName;

            var row = 8;
            foreach (var session in sessions)
            {
                sessionsSheet.Cells[row, 1].Value = session.UserId;
                sessionsSheet.Cells[row, 2].Value = session.UserName;
                sessionsSheet.Cells[row, 3].Value = session.Language;
                sessionsSheet.Cells[row, 4].Value = UseUtc ? session.Start : session.Start.ToLocalTime();
                sessionsSheet.Cells[row, 5].Value = UseUtc ? session.End : session.End.ToLocalTime();
                sessionsSheet.Cells[row, 6].FormulaR1C1 = "=RC[-1]-RC[-2]";
                sessionsSheet.Cells[row, 7].FormulaR1C1 = "=RC[-1]*24*RC[2]";
                sessionsSheet.Cells[row, 8].Value = session.MaxViewers;
                sessionsSheet.Cells[row, 9].Value = session.AverageViewers;
                sessionsSheet.Cells[row, 10].Value = session.Observations;
                sessionsSheet.Cells[row, 11].Value = session.Title;

                ++row;
            }

            sessionsSheet.Cells[4, 2].FormulaR1C1 = $"=MIN(R8C4:R{row - 1}C4)";
            sessionsSheet.Cells[5, 2].FormulaR1C1 = $"=MAX(R8C5:R{row - 1}C5)";

            sessionsSheet.Cells[7, 1, row - 1, 11].AutoFilter = true;
            sessionsSheet.Cells[7, 2, row - 1, 11].AutoFitColumns();

            // The observations
            var observationsSheet = package.Workbook.Worksheets["Observations"];
            observationsSheet.Cells[2, 1].Value = gameName;

            row = 8;
            foreach (var observation in observations)
            {
                observationsSheet.Cells[row, 1].Value = observation.UserId;
                observationsSheet.Cells[row, 2].Value = observation.UserName;
                observationsSheet.Cells[row, 3].Value = observation.Language;
                observationsSheet.Cells[row, 4].Value = UseUtc ? observation.Start : observation.Start.ToLocalTime();
                observationsSheet.Cells[row, 5].Value = UseUtc ? observation.Moment : observation.Moment.ToLocalTime();
                observationsSheet.Cells[row, 6].FormulaR1C1 = "=RC[-1]-RC[-2]";
                observationsSheet.Cells[row, 7].Value = observation.Viewers;
                observationsSheet.Cells[row, 8].Value = observation.Title;

                ++row;
            }

            observationsSheet.Cells[4, 2].FormulaR1C1 = $"=MIN(R8C4:R{row - 1}C4)";
            observationsSheet.Cells[5, 2].FormulaR1C1 = $"=MAX(R8C5:R{row - 1}C5)";

            observationsSheet.Cells[7, 1, row - 1, 8].AutoFilter = true;
            observationsSheet.Cells[7, 2, row - 1, 8].AutoFitColumns();

            // Total Viewership 
            var viewershipSheet = package.Workbook.Worksheets["Viewership"];
            viewershipSheet.Cells[2, 1].Value = gameName;

            row = 8;
            foreach (var v in viewership)
            {
                viewershipSheet.Cells[row, 1].Value = UseUtc ? v.Hour : v.Hour.ToLocalTime();
                viewershipSheet.Cells[row, 2].Value = v.Streamers;
                viewershipSheet.Cells[row, 3].Value = v.AvgViewers;
                ++row;
            }

            var destFile = new FileInfo(ExcelFile);
            package.SaveAs(destFile);
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
                    BadDataFound = _ => { }
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
                Start = Period switch
                {
                    ReportPeriod.Day => End.Value.AddDays(-1),
                    ReportPeriod.Week => End.Value.AddDays(-7),
                    ReportPeriod.Month => End.Value.AddMonths(-1),
                    _ => throw new ArgumentOutOfRangeException(nameof(Period), Period, "Invalid Period!")
                };
            }
            else if (Start != null && End == null && Period != null)
            {
                End = Period switch
                {
                    ReportPeriod.Day => Start.Value.AddDays(1),
                    ReportPeriod.Week => Start.Value.AddDays(7),
                    ReportPeriod.Month => Start.Value.AddMonths(1),
                    _ => throw new ArgumentOutOfRangeException(nameof(Period), Period, "Invalid Period!")
                };
            }
            else if (Start == null && End != null && Period != null)
            {
                Start = Period switch
                {
                    ReportPeriod.Day => End.Value.AddDays(-1),
                    ReportPeriod.Week => End.Value.AddDays(-7),
                    ReportPeriod.Month => End.Value.AddMonths(-1),
                    _ => throw new ArgumentOutOfRangeException(nameof(Period), Period, "Invalid Period!")
                };
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

        private record Hourly(DateTime Hour, int Streamers, int AvgViewers);
        
        private class Session
        {
            private readonly Dictionary<string, int> _titles = new(2);
            private int _cumulativeViewers;

            public long UserId { get; init; }
            public string UserName { get; init; }
            public string Language { get; init; }
            public DateTime Start { get; init; }

            public string Title => _titles.OrderByDescending(kv => kv.Value).FirstOrDefault().Key;

            public DateTime End { get; private set; }
            public int Observations { get; private set; }
            public int AverageViewers => _cumulativeViewers / Observations;
            public int MaxViewers { get; private set; }

            public TimeSpan Duration => End - Start;

            public int ViewersMinutes => (int) (AverageViewers * Duration.TotalMinutes);

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

                _cumulativeViewers += o.Viewers;
            }
        }

        [PublicAPI]
        private record Observation
        {
            [Name("User Id")] public long UserId { get; init; }

            [Name("User Name")] public string UserName { get; init; }

            public string Language { get; init; }

            public int Viewers { get; init; }

            [Name("Started At")] public DateTime Start { get; init; }

            [Name("Running Minutes")] public int Minutes { get; init; }


            public string Title { get; init; }

            public DateTime Moment => Start.AddMinutes(Minutes);

            public DateTime MomentHour
            {
                get
                {
                    var m = Moment;
                    return new DateTime(m.Year, m.Month, m.Day, m.Hour, 0, 0, m.Kind);
                }
            }
        }
    }
}