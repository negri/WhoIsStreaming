using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CliFx;
using Microsoft.Extensions.DependencyInjection;
using Negri.Twitch.Commands;

namespace Negri.Twitch
{
    abstract class Program
    {
        private static async Task<int> Main()
        {

            var appSettings = GetAppSettings();
            if (appSettings == null)
            {
                await Console.Error.WriteLineAsync("The required file secrets.json could not be found. Put it alongside the application, or on the start path of the application.");
                return (int)ReturnCode.NoSecrets;
            }

            var services = new ServiceCollection();

            services.AddTransient(p =>
                new SearchGame(appSettings));

            services.AddTransient(p =>
                new Collect(appSettings));

            services.AddTransient(p =>
                new Report(appSettings));

            var serviceProvider = services.BuildServiceProvider();

            return await new CliApplicationBuilder()
                .AddCommandsFromThisAssembly()
                .UseTypeActivator(serviceProvider.GetService)
                .Build()
                .RunAsync();

        }

        private static AppSettings GetAppSettings()
        {
            if (!File.Exists("secrets.json"))
            {
                return null;
            }

            var content = File.ReadAllText("secrets.json", Encoding.UTF8);

            var o = new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            var settings = JsonSerializer.Deserialize<AppSettings>(content, o);

            return settings;
        }

        
    }
}
