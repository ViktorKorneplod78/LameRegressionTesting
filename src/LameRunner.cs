using System.Diagnostics;

namespace LameRegression;

// Запуск lame.exe и замер времени
public static class LameRunner
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    public record EncodeResult(double ElapsedMs, double CpuMs);

    // Возвращает первую строку вывода `lame --version`
    public static string GetVersion(string lameExe)
    {
        var (exitCode, stdout, _) = Run(lameExe, new[] { "--version" }, out _);
        string firstLine = stdout.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? "";
        // "LAME 64bits version 4.0 (https://lame.sourceforge.io)" -> "LAME 64bits version 4.0"
        int urlStart = firstLine.IndexOf(" (", StringComparison.Ordinal);
        if (urlStart > 0) firstLine = firstLine[..urlStart];
        return exitCode == 0 ? firstLine : $"unknown (exit code {exitCode})";
    }

    // Кодирует input в output. При ошибке бросает исключение
    public static EncodeResult Encode(string lameExe, string[] options, string input, string output)
    {
        // -S (silent): без прогресс-бара, чтобы вывод в консоль не влиял на время
        var args = new List<string> { "-S" };
        args.AddRange(options);
        args.Add(input);
        args.Add(output);

        var (exitCode, _, stderr) = Run(lameExe, args, out var timing);
        if (exitCode != 0)
            throw new Exception($"lame exited with code {exitCode}: {stderr.Trim()}");
        if (!File.Exists(output))
            throw new Exception($"lame did not create output file {output}");
        return timing;
    }

    private static (int ExitCode, string Stdout, string Stderr) Run(
        string exe, IEnumerable<string> args, out EncodeResult timing)
    {
        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        var sw = Stopwatch.StartNew();
        using var process = Process.Start(psi) ?? throw new Exception($"Cannot start {exe}");
        // Читаем потоки асинхронно, иначе процесс может зависнуть на заполненном буфере
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(Timeout))
        {
            process.Kill(entireProcessTree: true);
            throw new Exception($"{exe} timed out after {Timeout.TotalSeconds} s");
        }
        sw.Stop();

        timing = new EncodeResult(sw.Elapsed.TotalMilliseconds, process.TotalProcessorTime.TotalMilliseconds);
        return (process.ExitCode, stdoutTask.Result, stderrTask.Result);
    }
}
