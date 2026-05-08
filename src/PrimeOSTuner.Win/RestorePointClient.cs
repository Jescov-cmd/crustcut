using System.Management;

namespace PrimeOSTuner.Win;

public sealed class RestorePointClient : IRestorePointClient
{
    public bool IsAvailable()
    {
        try
        {
            var scope = new ManagementScope(@"\\.\root\default");
            scope.Connect();
            return scope.IsConnected;
        }
        catch { return false; }
    }

    public bool TryCreate(string description, out string? error)
    {
        error = null;
        try
        {
            var path = new ManagementPath(@"\\.\root\default:SystemRestore");
            using var sysRestore = new ManagementClass(path);
            var args = sysRestore.GetMethodParameters("CreateRestorePoint");
            args["Description"] = description;
            args["RestorePointType"] = 12; // MODIFY_SETTINGS
            args["EventType"] = 100;       // BEGIN_SYSTEM_CHANGE
            var result = sysRestore.InvokeMethod("CreateRestorePoint", args, null);
            var rc = Convert.ToInt32(result["ReturnValue"]);
            if (rc == 0) return true;
            error = $"CreateRestorePoint returned {rc}";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
