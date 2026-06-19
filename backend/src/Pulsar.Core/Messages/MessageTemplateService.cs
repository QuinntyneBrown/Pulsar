using System.Text.Json;
using System.Text.Json.Serialization;
using Pulsar.Contracts;

namespace Pulsar.Core.Messages;

/// <summary>
/// Bridges between editable JSON in the UI and concrete message instances. This
/// is the <em>only</em> place Pulsar touches the shape of a message, and it does
/// so generically via reflection — it never needs to know the type at compile
/// time.
/// </summary>
public sealed class MessageTemplateService
{
    /// <summary>
    /// JSON settings used for the editor view. Web defaults give camelCase +
    /// case-insensitive matching; enums are written as readable strings.
    /// </summary>
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Serializes a fresh template instance for a descriptor to indented JSON.</summary>
    public string CreateTemplateJson(MessageDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        object instance;
        try
        {
            instance = descriptor.CreateTemplate()
                ?? throw new MessageEditException($"The template factory for '{descriptor.Key}' returned null.");
        }
        catch (MessageEditException) { throw; }
        catch (Exception ex)
        {
            throw new MessageEditException($"The template factory for '{descriptor.Key}' threw: {ex.Message}", ex);
        }

        return JsonSerializer.Serialize(instance, instance.GetType(), JsonOptions);
    }

    /// <summary>Turns edited JSON back into an instance of the descriptor's message type.</summary>
    public object Rehydrate(string json, MessageDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (string.IsNullOrWhiteSpace(json))
            throw new MessageEditException("Payload JSON must not be empty.");

        try
        {
            return JsonSerializer.Deserialize(json, descriptor.MessageType, JsonOptions)
                ?? throw new MessageEditException("Payload JSON deserialized to null.");
        }
        catch (JsonException ex)
        {
            throw new MessageEditException($"Payload JSON is not valid for '{descriptor.DisplayName}': {ex.Message}", ex);
        }
    }
}
