using LinkLogistics.Core.Abstractions;
using LinkLogistics.Infrastructure.Documents;
using LinkLogistics.Infrastructure.Persistence;
using LinkLogistics.Infrastructure.Security;
using LinkLogistics.Infrastructure.Storage;
using QuestPDF.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LinkLogistics.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Configure(o => o.ConnectionString =
                configuration.GetConnectionString("Default")
                ?? configuration["ConnectionStrings:Default"]
                ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured."))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        QuestPDF.Settings.License = LicenseType.Community;

        services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IObjectStorage, MinioObjectStorage>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IDeliveryRepository, DeliveryRepository>();
        services.AddScoped<IProofRepository, ProofRepository>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();
        services.AddScoped<IProofDocumentService, ProofDocumentService>();

        return services;
    }

    public static JwtOptions GetJwtOptions(this IServiceProvider provider) =>
        provider.GetRequiredService<IOptions<JwtOptions>>().Value;
}
