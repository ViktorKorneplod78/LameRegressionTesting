using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace LameRegression;

// Сведения о тестовом окружении для отчёта
public static class EnvironmentInfo
{
    public static List<(string Name, string Value)> Collect() => new()
    {
        ("Компьютер", Environment.MachineName),
        ("ОС", $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})"),
        ("Процессор", CpuName()),
        ("Логических ядер", Environment.ProcessorCount.ToString()),
        ("Оперативная память", $"{GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024.0 / 1024 / 1024:F1} GB"),
        (".NET", RuntimeInformation.FrameworkDescription),
        ("Пользователь", Environment.UserName),
    };

    private static string CpuName()
    {
        if (!OperatingSystem.IsWindows())
            return RuntimeInformation.ProcessArchitecture.ToString();
        using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
        return key?.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? "unknown";
    }
}
