using Pulsar.Contracts;

namespace Pulsar.SampleMessages;

// These POCOs are deliberately generic example messages. Their property
// initializers double as the editor template, so what you see in the UI is a
// realistic, ready-to-send message. Replace this file with references to your
// own message types when you build a real plugin.

public enum Severity { Info, Warning, Critical }

[PublishChannel("telemetry.heartbeat", MessageCategory.Telemetry, DisplayName = "Heartbeat")]
public sealed class HeartbeatTelemetry
{
    public string DeviceId { get; set; } = "device-001";
    public long SequenceNumber { get; set; }
    public string Status { get; set; } = "Nominal";
    public double UptimeSeconds { get; set; } = 3600;
}

[PublishChannel("telemetry.temperature", MessageCategory.Telemetry, DisplayName = "Temperature Reading")]
public sealed class TemperatureReading
{
    public string SensorId { get; set; } = "sensor-temp-1";
    public double Celsius { get; set; } = 21.5;
    public double[] RecentSamples { get; set; } = { 21.3, 21.4, 21.5 };
}

[PublishChannel("telemetry.battery", MessageCategory.Telemetry, DisplayName = "Battery Status")]
public sealed class BatteryTelemetry
{
    public string PackId { get; set; } = "pack-a";
    public double Percentage { get; set; } = 87.5;
    public double Voltage { get; set; } = 28.4;
    public bool Charging { get; set; }
}

[PublishChannel("events.alert", MessageCategory.Event, DisplayName = "Operator Alert")]
public sealed class OperatorAlert
{
    public string Code { get; set; } = "INFO-100";
    public string Message { get; set; } = "Routine status update.";
    public Severity Severity { get; set; } = Severity.Info;
}

[PublishChannel("events.mode-changed", MessageCategory.Event, DisplayName = "Mode Changed")]
public sealed class ModeChangedEvent
{
    public string From { get; set; } = "Idle";
    public string To { get; set; } = "Active";
    public string Reason { get; set; } = "Operator command";
}

[PublishChannel("faults.subsystem", MessageCategory.Fault, DisplayName = "Subsystem Fault")]
public sealed class SubsystemFault
{
    public string Subsystem { get; set; } = "power";
    public string FaultCode { get; set; } = "F-205";
    public string Description { get; set; } = "Voltage out of expected range.";
    public Severity Severity { get; set; } = Severity.Critical;
}
