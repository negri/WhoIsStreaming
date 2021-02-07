# WhoIsStreaming
Create reports of who, and when, is streaming a given game on Twitch

## Configuration
To run the app, you need a Twitch developer account. Create it at [dev.twitch.tv](https://dev.twitch.tv/).

Next, make a text file called "secrets.json" on the same directory of the executable file. Insert your client ID and client secret in there like this:

```
{
  "ClientId": "Your Client Id",
  "ClientSecret": "Your Client Secret"
}
```

Those are secrets! Never ever share or publish on the Internet!

Then, you must install .NET 5.0 runtime. Download it at [Microsoft's website](https://dotnet.microsoft.com/download/dotnet/current/runtime). .NET is free and open source.

## Usage
This is a command line utility, which means you run it from your terminal (be it bash, cmd or PowerShell). You can also schedule the collect command to periodically retrieve and store data about streamers on a game using your operating system's scheduler.

### SearchGame
Usage:

`WhoIsStreaming SearchGame {GameName}`

This searches for games on Twitch with GameName. The result will be a table with the results of the search. The result will include the game ID and the game name, like this:
```
C:\WIS>WhoIsStreaming SearchGame "Team Fortress 2"
Search for 'Team Fortress 2' returned 2 results:
Id              Game
1631727329      Team Fortress 2 Arcade
16676           Team Fortress 2
```
If you wish to collect the data on the game, you need its ID.

### Collect
Usage:

`WhoIsStreaming Collect <gameID> [options]`

This collects data on who is streaming at the exact moment of the call and prints them to the screen. You can choose to save files which can later be used to create a report.

Options:
```
-d|--data-dir     The directory where to write collected data.
-t|--save-thumbs  If thumbnails should be saved. Default: "False".
-v|--min-viewers  Minimum numbers of viewers to collect. Default: "0".
--min-viewers-thumbs  Minimum numbers of viewers to save thumbnails. Default: "0".
```

### Report

Usage:

`WhoIsStreaming Report <gameID> --data-dir <value> [options]`

This uses the files created by the Collect command to create a report about all the stream sessions that appeared.

Options: 

```
-d|--data-dir     The directory where to read collected data. Mandatory.
-e|--excel-file   The Excel filename to write to.
--start           The start moment to report. Default = 7 days ago
--end             The end moment to stop reporting. Default = now
-p|--period       The most recent period to report. Valid values: "Day", "Week", "Month".
-u|--use-utc      All input and output dates are in UTC. Default: "False". If false, local time will be used.
```
