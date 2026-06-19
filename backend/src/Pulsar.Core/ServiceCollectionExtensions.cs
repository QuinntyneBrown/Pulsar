using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pulsar.Core.Activity;
using Pulsar.Core.Cyclic;
using Pulsar.Core.Messages;
using Pulsar.Core.Plugins;
using Pulsar.Core.Publishing;

namespace Pulsar.Core;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Pulsar domain services. The transport (Redis) and a live
    /// activity notifier are expected to be registered separately by the host.
    /// A no-op notifier is registered as a fallback.
    /// </summary>
    public static IServiceCollection AddPulsarCore(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<PluginLoader>();
        services.TryAddSingleton<PluginHost>();
        services.TryAddSingleton<IPluginHost>(sp => sp.GetRequiredService<PluginHost>());
        services.TryAddSingleton<MessageTemplateService>();
        services.TryAddSingleton<MessagePublishService>();
        services.TryAddSingleton<CyclicJobManager>();
        services.TryAddSingleton<PluginManagementService>();
        services.TryAddSingleton<IActivityNotifier, NullActivityNotifier>();
        return services;
    }
}
