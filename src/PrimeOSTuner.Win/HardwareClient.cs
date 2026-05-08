using LibreHardwareMonitor.Hardware;

namespace PrimeOSTuner.Win;

public sealed class HardwareClient : IHardwareClient
{
    private readonly Computer _computer = new()
    {
        IsCpuEnabled = true,
        IsGpuEnabled = true,
        IsMemoryEnabled = true,
        IsNetworkEnabled = true
    };

    public HardwareClient() => _computer.Open();

    public HardwareSnapshot Snapshot()
    {
        foreach (var hw in _computer.Hardware) hw.Update();

        var cpu = ReadFirst(HardwareType.Cpu, SensorType.Load, "CPU Total") ?? 0;
        var ramUsed = (ReadFirst(HardwareType.Memory, SensorType.Data, "Memory Used") ?? 0) * 1024L * 1024L * 1024L;
        var ramAvail = (ReadFirst(HardwareType.Memory, SensorType.Data, "Memory Available") ?? 0) * 1024L * 1024L * 1024L;
        var ramTotal = (long)(ramUsed + ramAvail);
        var gpuLoad = ReadAny(new[] { HardwareType.GpuNvidia, HardwareType.GpuAmd, HardwareType.GpuIntel }, SensorType.Load, "GPU Core") ?? 0;
        var gpuTemp = ReadAny(new[] { HardwareType.GpuNvidia, HardwareType.GpuAmd, HardwareType.GpuIntel }, SensorType.Temperature, "GPU Core") ?? 0;
        var netDown = (long)(ReadFirst(HardwareType.Network, SensorType.Throughput, "Download Speed") ?? 0);
        var netUp = (long)(ReadFirst(HardwareType.Network, SensorType.Throughput, "Upload Speed") ?? 0);

        return new HardwareSnapshot(cpu, (long)ramUsed, ramTotal, gpuLoad, gpuTemp, netDown, netUp);
    }

    private float? ReadFirst(HardwareType hwType, SensorType sensorType, string name)
    {
        foreach (var hw in _computer.Hardware)
            if (hw.HardwareType == hwType)
                foreach (var s in hw.Sensors)
                    if (s.SensorType == sensorType && s.Name == name) return s.Value;
        return null;
    }

    private float? ReadAny(HardwareType[] types, SensorType sensorType, string name)
    {
        foreach (var t in types)
        {
            var v = ReadFirst(t, sensorType, name);
            if (v.HasValue) return v;
        }
        return null;
    }

    public void Dispose() => _computer.Close();
}
