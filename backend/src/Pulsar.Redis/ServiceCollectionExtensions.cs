using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pulsar.Core.Transport;

namespace Pulsar.Redis;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the Redis Pub/Sub transport as the <see cref="IMessageTransport"/>.</summary>
    public static IServiceCollection AddPulsarRedis(this IServiceCollection services)
    {
        services.TryAddSingleton<RedisMessageTransport>();
        services.TryAddSingleton<IMessageTransport>(sp => sp.GetRequiredService<RedisMessageTransport>());
        return services;
    }
}
