using Cps.S3Spike.Clients;
using Cps.S3Spike.Extensions;
using Cps.S3Spike.Middleware;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

using var loggerFactory = LoggerFactory.Create(configure => configure.AddConsole());
var logger = loggerFactory.CreateLogger("Configuration");

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(webApp =>
    {
        // Enable request body buffering to support proxy retries for large file uploads.
        // This must run BEFORE the YARP proxy processes the request.
        webApp.UseMiddleware<RequestBufferingMiddleware>();
    })
    .ConfigureLogging((context, logging) =>
    {
        // Clear providers to avoid conflicts with default filtering
        logging.ClearProviders();

        if (context.HostingEnvironment.IsDevelopment())
        {
            logging.AddConsole();
        }

        // Read minimum log level from configuration, fallback to Information if not set or invalid
        var logLevelString = context.Configuration["Logging:LogLevel:Default"];
        if (!Enum.TryParse<LogLevel>(logLevelString, true, out var minLevel))
        {
            minLevel = LogLevel.Information;
        }
        logging.SetMinimumLevel(minLevel);
    })
    .ConfigureServices((context, services) =>
    {
        // Get configuration for service registrations
        var configuration = context.Configuration;

        services
            .AddApplicationInsightsTelemetryWorkerService()
            .ConfigureFunctionsApplicationInsights();
        
        // Increase Kestrel request body limit to match our upload limit (1 GB)
        services.Configure<KestrelServerOptions>(options =>
        {
            //options.Limits.MaxRequestBodySize = 1073741824; // 1 GB
            options.Limits.MaxRequestBodySize = null;
        });

        //services.AddSingleton<IAuthorizationValidator, AuthorizationValidator>();

        services.AddSingleton(provider =>
        {
            return new ConfigurationManager<OpenIdConnectConfiguration>(
                        $"https://login.microsoftonline.com/{configuration["TenantId"]}/v2.0/.well-known/openid-configuration",
                        new OpenIdConnectConfigurationRetriever(),
                        new HttpDocumentRetriever());
        });

        services.AddNetAppClient(configuration);
        services.AddS3HttpClient();
        services.AddSingleton<IS3Client, S3Client>();
    })
    .Build();

await host.RunAsync();
