using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectX.PM.Domain.Entities;

namespace ProjectX.PM.Infrastructure.Persistence;

public static class ApplicationInitializationExtensions
{
    public static async Task InitializePersistenceAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await dbContext.Database.MigrateAsync();
        await SeedProjectsAsync(dbContext);
    }

    private static async Task SeedProjectsAsync(ApplicationDbContext dbContext)
    {
        var now = DateTimeOffset.UtcNow;
        var seededProjects = SeedProjectCatalog.All;
        var seededCodes = seededProjects
            .Select(project => project.Code)
            .ToArray();
        var existingProjects = await dbContext.Projects
            .Where(project => seededCodes.Contains(project.Code))
            .ToDictionaryAsync(project => project.Code, StringComparer.OrdinalIgnoreCase);
        var hasChanges = false;

        foreach (var seededProject in seededProjects)
        {
            if (!existingProjects.TryGetValue(seededProject.Code, out var project))
            {
                dbContext.Projects.Add(new Project
                {
                    Id = seededProject.Id,
                    Code = seededProject.Code,
                    Name = seededProject.Name,
                    Description = seededProject.Description,
                    OwnerName = seededProject.OwnerName,
                    Status = seededProject.Status,
                    StartDate = null,
                    TargetDate = null,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });

                hasChanges = true;
                continue;
            }

            var projectChanged = false;

            if (!string.Equals(project.Name, seededProject.Name, StringComparison.Ordinal))
            {
                project.Name = seededProject.Name;
                projectChanged = true;
            }

            if (!string.Equals(project.Description, seededProject.Description, StringComparison.Ordinal))
            {
                project.Description = seededProject.Description;
                projectChanged = true;
            }

            if (!string.Equals(project.OwnerName, seededProject.OwnerName, StringComparison.Ordinal))
            {
                project.OwnerName = seededProject.OwnerName;
                projectChanged = true;
            }

            if (project.Status != seededProject.Status)
            {
                project.Status = seededProject.Status;
                projectChanged = true;
            }

            if (project.StartDate is not null)
            {
                project.StartDate = null;
                projectChanged = true;
            }

            if (project.TargetDate is not null)
            {
                project.TargetDate = null;
                projectChanged = true;
            }

            if (!string.Equals(project.Code, seededProject.Code, StringComparison.Ordinal))
            {
                project.Code = seededProject.Code;
                projectChanged = true;
            }

            if (projectChanged)
            {
                project.UpdatedAtUtc = now;
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            await dbContext.SaveChangesAsync();
        }
    }
}
