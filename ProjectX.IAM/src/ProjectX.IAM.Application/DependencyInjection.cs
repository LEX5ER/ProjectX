using Microsoft.Extensions.DependencyInjection;
using ProjectX.IAM.Application.Management;

namespace ProjectX.IAM.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IIdentityAdministrationService, IdentityAdministrationService>();
        return services;
    }
}
