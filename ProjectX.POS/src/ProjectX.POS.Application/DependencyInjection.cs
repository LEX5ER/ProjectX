using Microsoft.Extensions.DependencyInjection;
using ProjectX.POS.Application.Customers;
using ProjectX.POS.Application.Dashboard;
using ProjectX.POS.Application.Products;
using ProjectX.POS.Application.Sales;

namespace ProjectX.POS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ICustomersService, CustomersService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IProductsService, ProductsService>();
        services.AddScoped<ISalesService, SalesService>();
        return services;
    }
}
