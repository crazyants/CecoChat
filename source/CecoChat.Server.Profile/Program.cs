using System;
using System.Reflection;
using CecoChat.Serilog;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace CecoChat.Server.Profile
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            SerilogConfiguration.Setup(Assembly.GetEntryAssembly());
            ILogger logger = Log.ForContext(typeof(Program));

            try
            {
                logger.Information("Starting...");
                CreateHostBuilder(args).Build().Run();
            }
            catch (Exception exception)
            {
                logger.Fatal(exception, "Unexpected failure.");
            }
            finally
            {
                logger.Information("Ended.");
                Log.CloseAndFlush();
            }
        }

        private static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host
                .CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureAppConfiguration(configurationBuilder =>
                {
                    configurationBuilder.AddEnvironmentVariables(prefix: "CECOCHAT_");
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
        }
    }
}