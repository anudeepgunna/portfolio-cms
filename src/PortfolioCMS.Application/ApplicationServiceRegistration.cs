using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using PortfolioCMS.Application.Common.Behaviours;

namespace PortfolioCMS.Application;

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Register all MediatR handlers from this assembly
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(ApplicationServiceRegistration).Assembly));

        // Register the validation pipeline — runs before every handler
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));

        // Register all FluentValidation validators from this assembly
        services.AddValidatorsFromAssembly(typeof(ApplicationServiceRegistration).Assembly);

        return services;
    }
}
