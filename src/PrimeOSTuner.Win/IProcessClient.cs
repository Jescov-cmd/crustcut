namespace PrimeOSTuner.Win;

public interface IProcessClient
{
    void TrimWorkingSet(int processId);
    int TrimAllUserProcesses();
}
