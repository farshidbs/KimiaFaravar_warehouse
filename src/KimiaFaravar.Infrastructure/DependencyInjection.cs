using KimiaFaravar.Application.Security;
using KimiaFaravar.Domain.Identity;
using KimiaFaravar.Infrastructure.Persistence;
using KimiaFaravar.Infrastructure.Security;
using KimiaFaravar.Infrastructure.JsonStorage;
using KimiaFaravar.Application.Inventory;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KimiaFaravar.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        var provider = configuration["Storage:Provider"] ?? "SqlServer";
        if (provider.Equals("Json", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<JsonAuthenticationService>();
            services.AddScoped<IAuthenticationService>(sp => sp.GetRequiredService<JsonAuthenticationService>());
            services.AddScoped<IStorageInitializer>(sp => sp.GetRequiredService<JsonAuthenticationService>());
            services.AddScoped<ISessionValidator>(sp => sp.GetRequiredService<JsonAuthenticationService>());
            services.AddScoped<IAccessControlQueryService>(sp => sp.GetRequiredService<JsonAuthenticationService>());
            services.AddScoped<IInventoryMvpService, JsonInventoryMvpService>();
        }
        else
        {
            var connection = configuration.GetConnectionString("InventoryDatabase") ?? throw new InvalidOperationException("ConnectionStrings:InventoryDatabase تنظیم نشده است.");
            services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connection, sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));
            services.AddScoped<IAuthenticationService, AuthenticationService>();
            services.AddScoped<IStorageInitializer, DatabaseInitializer>();
            services.AddScoped<SqlAccessControlService>();
            services.AddScoped<ISessionValidator>(sp => sp.GetRequiredService<SqlAccessControlService>());
            services.AddScoped<IAccessControlQueryService>(sp => sp.GetRequiredService<SqlAccessControlService>());
        }
        return services;
    }
}
