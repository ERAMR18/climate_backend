using System.IO.Ports;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Climate.ArduinoBridge;

public interface ISerialDataSource
{
    IAsyncEnumerable<string> ReadLinesAsync(CancellationToken cancellationToken);
}

public sealed class SerialDataSource(IOptions<BridgeOptions> options) : ISerialDataSource
{
    public async IAsyncEnumerable<string> ReadLinesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        BridgeOptions config = options.Value;
        if (config.Arduino.Simulate)
        {
            long sequence = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                yield return JsonSerializer.Serialize(new SerialReading(1, config.Device.DeviceId, "TEMPERATURE",
                    30 + (decimal)(10 * Math.Sin(sequence * 0.15)), "C", sequence++, null, "SIMULATED"), Protocol.Json);
                await Task.Delay(2000, cancellationToken);
            }
            yield break;
        }
        string portName = config.Arduino.PortName;
        if (string.IsNullOrWhiteSpace(portName))
        {
            string[] ports = SerialPort.GetPortNames();
            if (ports.Length != 1) throw new IOException("Configure Arduino:PortName; automatic detection requires exactly one serial port.");
            portName = ports[0];
        }
        using SerialPort port = new(portName, config.Arduino.BaudRate) { NewLine = "\n", ReadTimeout = 1000 };
        port.Open();
        using CancellationTokenRegistration registration = cancellationToken.Register(port.Close);
        // Read byte by byte to bound malformed frames even when newline never arrives.
        using StreamReader reader = new(port.BaseStream);
        char[] character = new char[1];
        System.Text.StringBuilder frame = new();
        while (!cancellationToken.IsCancellationRequested)
        {
            int count = await reader.ReadAsync(character.AsMemory(), cancellationToken);
            if (count == 0) throw new IOException("Serial port disconnected.");
            if (character[0] == '\n')
            {
                yield return frame.ToString().TrimEnd('\r');
                frame.Clear();
            }
            else
            {
                frame.Append(character[0]);
                if (frame.Length > 4096) throw new IOException("Serial frame exceeds maximum length; reconnecting.");
            }
        }
    }
}
