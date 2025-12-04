using System.Net.Http.Headers;
using Cps.S3Spike.Clients;
using Cps.S3Spike.Factories;
using Cps.S3Spike.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Cps.S3Spike.Extensions;

public static class IServiceCollectionExtension
{
    public static void AddNetAppClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<NetAppOptions>(configuration.GetSection(nameof(NetAppOptions)));
        services.AddHttpClient<INetAppClient, NetAppClient>(AddNetAppClient)
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; }
        })
          .SetHandlerLifetime(TimeSpan.FromMinutes(5));
        services.AddTransient<INetAppRequestFactory, NetAppRequestFactory>();
    }

    public static void AddS3HttpClient(this IServiceCollection services)
    {
        services.AddHttpClient<IS3HttpClient, S3HttpClient>(AddS3HttpClient)
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; }
        })
          .SetHandlerLifetime(TimeSpan.FromMinutes(5));
    }

    internal static void AddNetAppClient(IServiceProvider configuration, HttpClient client)
    {
        var opts = configuration.GetService<IOptions<NetAppOptions>>()?.Value ?? throw new ArgumentNullException(nameof(NetAppOptions));
        client.BaseAddress = new Uri(opts.BaseUrl);
        client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
    }

    internal static void AddS3HttpClient(IServiceProvider configuration, HttpClient client)
    {
        client.BaseAddress = new Uri("https://10.4.16.19/");
        client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
    }
}