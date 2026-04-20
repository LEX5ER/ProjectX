using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProjectX.POS.Application.Abstractions;
using ProjectX.POS.Application.Auth;
using ProjectX.POS.Infrastructure.Auth;
using ProjectX.POS.Infrastructure.Persistence;

namespace ProjectX.POS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ApplicationDatabaseDefaults.ConnectionStringName)
            ?? ApplicationDatabaseDefaults.DefaultConnectionString;

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseSqlServer(connectionString);
        });
        services.AddScoped<IApplicationDbContext>(serviceProvider => serviceProvider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IRequestContextAccessor, HttpRequestContextAccessor>();
        services.AddHttpClient<IIamAuthorizationContextService, IamAuthorizationContextService>((serviceProvider, httpClient) =>
        {
            var iamAuthOptions = configuration.GetSection(IamAuthenticationOptions.SectionName).Get<IamAuthenticationOptions>()
                ?? throw new InvalidOperationException("IAM authentication configuration is missing.");

            if (string.IsNullOrWhiteSpace(iamAuthOptions.ApiBaseUrl))
            {
                throw new InvalidOperationException("IAM API base URL is missing.");
            }

            httpClient.BaseAddress = new Uri(iamAuthOptions.ApiBaseUrl.TrimEnd('/'));
            httpClient.Timeout = TimeSpan.FromSeconds(5);
        });

        return services;
    }
}
