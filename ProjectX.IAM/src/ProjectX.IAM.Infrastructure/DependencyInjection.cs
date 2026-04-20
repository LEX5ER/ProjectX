using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProjectX.IAM.Application.Abstractions;
using ProjectX.IAM.Application.Auth;
using ProjectX.IAM.Application.Management;
using ProjectX.IAM.Domain.Entities;
using ProjectX.IAM.Infrastructure.Auth;
using ProjectX.IAM.Infrastructure.Persistence;

namespace ProjectX.IAM.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ApplicationDatabaseDefaults.ConnectionStringName)
            ?? ApplicationDatabaseDefaults.DefaultConnectionString;

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<SeedIdentityOptions>(configuration.GetSection(SeedIdentityOptions.SectionName));
        services.Configure<PmProjectCatalogOptions>(configuration.GetSection(PmProjectCatalogOptions.SectionName));

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseSqlServer(connectionString);
        });
        services.AddScoped<IApplicationDbContext>(serviceProvider => serviceProvider.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IRequestContextAccessor, HttpRequestContextAccessor>();
        services.AddScoped<IUserPasswordService, UserPasswordService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddHttpClient<IPmProjectCatalogService, PmProjectCatalogService>((serviceProvider, httpClient) =>
        {
            var options = configuration.GetSection(PmProjectCatalogOptions.SectionName).Get<PmProjectCatalogOptions>()
                ?? throw new InvalidOperationException("PM project catalog configuration is missing.");

            if (string.IsNullOrWhiteSpace(options.ApiBaseUrl))
            {
                throw new InvalidOperationException("PM project catalog API base URL is missing.");
            }

            httpClient.BaseAddress = new Uri(options.ApiBaseUrl.TrimEnd('/'));
            httpClient.Timeout = TimeSpan.FromSeconds(5);
        });

        return services;
    }
}
