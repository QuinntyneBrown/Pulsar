using System.Reflection;
using System.Text;
using Pulsar.Contracts;

namespace Pulsar.SampleMessages;

/// <summary>
/// The plugin Pulsar discovers. It builds its catalog by scanning this assembly
/// for types annotated with <see cref="PublishChannelAttribute"/> and pairs them
/// with <see cref="SampleJsonSerializer"/>.
/// </summary>
public sealed class SamplePlugin : IPulsarPlugin
{
    public string Name => "Sample Messages";

    public IMessageCatalog Catalog { get; } = BuildCatalog();

    public IMessageSerializer Serializer { get; } = new SampleJsonSerializer();

    private static MessageCatalog BuildCatalog()
    {
        var catalog = new MessageCatalog();

        var annotated =
            from type in typeof(SamplePlugin).Assembly.GetTypes()
            let attr = type.GetCustomAttribute<PublishChannelAttribute>(inherit: false)
            where attr is not null && type is { IsClass: true, IsAbstract: false }
            orderby attr.Category, type.Name
            select (type, attr);

        foreach (var (type, attr) in annotated)
        {
            var local = type; // capture per-iteration for the closure
            catalog.Add(new MessageDescriptor(
                key: attr.Key ?? local.Name,
                displayName: attr.DisplayName ?? Humanize(local.Name),
                category: attr.Category,
                messageType: local,
                defaultChannel: attr.Channel,
                createTemplate: () => Activator.CreateInstance(local)!));
        }

        return catalog;
    }

    private static string Humanize(string name)
    {
        var sb = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
                sb.Append(' ');
            sb.Append(name[i]);
        }
        return sb.ToString();
    }
}
