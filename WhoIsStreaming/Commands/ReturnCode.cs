namespace Negri.Twitch.Commands
{
    public enum ReturnCode
    {
        NotImplemented = 1,
        DataDirectoryDoesNotExists = 2,
        DataDirectoryNotWritable = 3,
        GameNotFound = 4,
        ThreeDateParameters = 5,
        MissingDateParameters = 6,
        InvalidDateParameters = 7,
        NoObservations = 8,
        NoSecrets = 9
    }
}