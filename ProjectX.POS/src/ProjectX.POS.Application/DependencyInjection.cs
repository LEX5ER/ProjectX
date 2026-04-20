using Microsoft.Extensions.DependencyInjection;
using ProjectX.POS.Application.Products;

namespace ProjectX.POS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IProductsService, ProductsService>();
        return services;
    }
}
