namespace LameRegression;

// Классы с результатами сценария (только данные, без логики запуска).
// Заполняются в ScenarioRunner, сохраняются в results\<имя сценария>.json,
// читаются обратно при построении отчёта в ReportWriter.
public class ScenarioResult
{
    public string Name { get; set; } = "";
    public string InputFile { get; set; } = "";
    public string Options { get; set; } = "";
    public double AudioSeconds { get; set; }
    public int Iterations { get; set; }

    public VersionResult Old { get; set; } = new();
    public VersionResult New { get; set; } = new();

    // Файлы mp3 побайтово совпадают целиком
    public bool FilesIdentical { get; set; }

    // Совпадают аудиокадры (без первого кадра с Xing/LAME-тегом, где записана версия)
    public bool AudioIdentical { get; set; }

    // Разница медианного времени new относительно old, в процентах (+ значит медленнее)
    public double SpeedDiffPercent =>
        Old.MedianMs > 0 ? Math.Round((New.MedianMs - Old.MedianMs) / Old.MedianMs * 100, 1) : 0;

    // Текст ошибки, если сценарий не удалось выполнить
    public string? Error { get; set; }
}

// Результаты кодирования одной версией LAME
public class VersionResult
{
    public string Version { get; set; } = "";
    public string LamePath { get; set; } = "";
    public string OutputFile { get; set; } = "";
    public long OutputSize { get; set; }

    // SHA-256 всего файла и SHA-256 без кадра с LAME-тегом
    public string Sha256 { get; set; } = "";
    public string AudioSha256 { get; set; } = "";

    // Время каждого замера и процессорное время процесса, мс
    public List<double> TimesMs { get; set; } = new();
    public List<double> CpuMs { get; set; } = new();

    public double MinMs => TimesMs.Count > 0 ? TimesMs.Min() : 0;
    public double AvgMs => TimesMs.Count > 0 ? Math.Round(TimesMs.Average(), 1) : 0;
    public double MedianMs => Median(TimesMs);
    public double MedianCpuMs => Median(CpuMs);

    private static double Median(List<double> values)
    {
        if (values.Count == 0) return 0;
        var sorted = values.OrderBy(v => v).ToList();
        int mid = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[mid] : Math.Round((sorted[mid - 1] + sorted[mid]) / 2, 1);
    }
}
