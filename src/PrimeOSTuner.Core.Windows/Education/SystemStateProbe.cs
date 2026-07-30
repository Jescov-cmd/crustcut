using System.Management;

namespace PrimeOSTuner.Core.Education;

/// <summary>The detected on/off state of the setting a guide teaches.</summary>
public enum DetectedState { Unknown, Enabled, Disabled }

/// <summary>
/// Best-effort detection of whether a guide's tweak is already applied.
/// Only XMP/EXPO is cheap and reliable enough to ship; other guides report
/// Unknown by design (per the 101-tab brief).
/// </summary>
public static class SystemStateProbe
{
    /// <summary>
    /// Detects whether an XMP/EXPO memory profile appears to be active by reading
    /// the running RAM speed via WMI. Never throws — returns Unknown on any failure.
    /// </summary>
    public static DetectedState DetectMemoryProfile()
    {
        try
        {
            uint configured = 0, rated = 0, ddrType = 0;
            using var searcher = new ManagementObjectSearcher(
                "SELECT ConfiguredClockSpeed, Speed, SMBIOSMemoryType FROM Win32_PhysicalMemory");
            foreach (ManagementBaseObject mo in searcher.Get())
            {
                configured = Math.Max(configured, ToUInt(mo["ConfiguredClockSpeed"]));
                rated = Math.Max(rated, ToUInt(mo["Speed"]));
                ddrType = Math.Max(ddrType, ToUInt(mo["SMBIOSMemoryType"]));
            }
            return ClassifyMemoryProfile(configured, rated, ddrType);
        }
        catch
        {
            return DetectedState.Unknown;
        }
    }

    /// <summary>
    /// Pure classification, separated from the WMI query so it can be tested.
    /// A profile is "Enabled" when the running speed is clearly above the JEDEC
    /// stock ceiling for that memory generation, or when the rated speed is met.
    /// </summary>
    public static DetectedState ClassifyMemoryProfile(uint configuredMhz, uint ratedMhz, uint smbiosMemoryType)
    {
        if (configuredMhz == 0) return DetectedState.Unknown;

        // A rated speed clearly above the running speed means the kit is running
        // below its rating — the profile is not applied.
        if (ratedMhz > configuredMhz + 100) return DetectedState.Disabled;

        // Otherwise compare the running speed to the JEDEC stock ceiling for this
        // memory generation (SMBIOS type: 24 = DDR3, 26 = DDR4, 34 = DDR5).
        uint jedecCeiling = smbiosMemoryType switch
        {
            34 => 5600,
            26 => 2666,
            24 => 1600,
            _  => 0,
        };
        if (jedecCeiling == 0) return DetectedState.Unknown;

        return configuredMhz > jedecCeiling ? DetectedState.Enabled : DetectedState.Disabled;
    }

    private static uint ToUInt(object? value)
    {
        try { return value is null ? 0u : Convert.ToUInt32(value); }
        catch { return 0u; }
    }
}
