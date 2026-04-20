using Microsoft.Extensions.DependencyInjection;
using ProjectX.PM.Application.Projects;

namespace ProjectX.PM.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IProjectsService, ProjectsService>();
        return services;
    }
}
