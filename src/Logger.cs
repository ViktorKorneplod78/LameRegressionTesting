namespace LameRegression;

// Простой лог: пишет одновременно в консоль и в файл run.log директории прогона
public static class Logger
{
    private static string? _file;

    public static void Init(string file) => _file = file;

    public static void Info(string message) => Write("INFO ", message);
    public static void Warn(string message) => Write("WARN ", message);
    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
        Console.WriteLine(line);
        if (_file != null)
            File.AppendAllText(_file, line + Environment.NewLine);
    }
}
