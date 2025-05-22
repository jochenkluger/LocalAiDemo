using LocalAiDemo.Shared.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LocalAiDemo.Shared.Extensions
{
    public static class ConfigurationExtensions
    {
        public static IServiceCollection AddAppConfiguration(this IServiceCollection services,
            IConfiguration configuration)
        {
            var appConfig = new AppConfiguration();
            configuration.GetSection("AppConfiguration").Bind(appConfig);

            // Register as singleton
            services.AddSingleton(appConfig);

            return services;
        }
    }
}