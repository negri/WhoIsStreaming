using System;
using System.Net.Http;

namespace Negri.Twitch.Api
{
    public static class WebApiRetryPolicy
    {
        private static bool IsTransientError(Exception ex)
        {
            if (ex is HttpRequestException)
            {
                return true;
            }
            return false;
        }

        public static T ExecuteAction<T>(Func<T> func, int? maxTries = null,
            Action<int, TimeSpan, Exception> beforeWaitAction = null)
        {
            var rp = new ExponentialRetryPolicy(TimeSpan.FromMilliseconds(100));
            return rp.ExecuteAction<T>(func, maxTries ?? 10, isTransientError: IsTransientError);
        }
    }
}