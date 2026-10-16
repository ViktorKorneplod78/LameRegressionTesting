using System.Text.Encodings.Web;
using System.Text.Json;

namespace LameRegression;

// Регрессионный тест двух версий LAME
//
//   LameRegression run    --run-dir DIR --name NAME --input WAV --options "LAME OPTIONS"
//                         --old OLD_LAME_EXE --new NEW_LAME_EXE [--iterations N]
//   LameRegression report --run-dir DIR [--threshold PERCENT]
//
// Команда run выполняет один сценарий и сохраняет результат в DIR\results\NAME.json
// Команда report собирает все результаты прогона в DIR\report.html
public static class Program
{
    // Отступы — чтобы JSON было удобно читать; UnsafeRelaxedJsonEscaping — чтобы символы
    // вроде + и ' записывались как есть, а не escape-последовательностями
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is not ("run" or "report"))
        {
            PrintUsage();
            return 2;
        }

        try
        {
            var options = ParseOptions(args.Skip(1).ToArray());
            string runDir = Path.GetFullPath(Required(options, "run-dir"));
            Directory.CreateDirectory(runDir);
            Logger.Init(Path.Combine(runDir, "run.log"));

            return args[0] == "run" ? RunScenario(options, runDir) : BuildReport(options, runDir);
        }
        catch (Exception ex)
        {
            Logger.Error(ex.Message);
            return 2;
        }
    }

    private static int RunScenario(Dictionary<string, string> options, string runDir)
    {
        string name = Required(options, "name");
        int iterations = int.Parse(options.GetValueOrDefault("iterations", "5"));
        if (iterations < 1)
            throw new ArgumentException("--iterations must be at least 1");

        Logger.Info($"===== Scenario {name} =====");
        var result = ScenarioRunner.Run(
            name,
            Path.GetFullPath(Required(options, "input")),
            Required(options, "options"),
            Path.GetFullPath(Required(options, "old")),
            Path.GetFullPath(Required(options, "new")),
            iterations,
            Path.Combine(runDir, "output", name));

        string resultsDir = Path.Combine(runDir, "results");
        Directory.CreateDirectory(resultsDir);
        File.WriteAllText(Path.Combine(resultsDir, name + ".json"), JsonSerializer.Serialize(result, JsonOptions));

        return result.Error == null ? 0 : 1;
    }

    private static int BuildReport(Dictionary<string, string> options, string runDir)
    {
        double threshold = double.Parse(options.GetValueOrDefault("threshold", "10"),
            System.Globalization.CultureInfo.InvariantCulture);

        string resultsDir = Path.Combine(runDir, "results");
        if (!Directory.Exists(resultsDir))
            throw new InvalidOperationException($"No scenario results found in {resultsDir}");

        var results = Directory.GetFiles(resultsDir, "*.json")
            .Order()
            .Select(file => JsonSerializer.Deserialize<ScenarioResult>(File.ReadAllText(file))!)
            .ToList();

        Logger.Info($"Building report for {results.Count} scenario(s)");
        string reportPath = Path.Combine(runDir, "report.html");
        ReportWriter.Write(reportPath, results, threshold, Path.Combine(runDir, "run.log"));
        Logger.Info($"Report saved: {reportPath}");

        return results.Any(r => r.Error != null) ? 1 : 0;
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var options = new Dictionary<string, string>();
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--") || i + 1 >= args.Length)
                throw new ArgumentException($"Unexpected argument: {args[i]}");
            // Значение берём как есть, даже если оно начинается с "-" (например, --options "-b 128")
            options[args[i][2..]] = args[++i];
        }
        return options;
    }

    private static string Required(Dictionary<string, string> options, string key) =>
        options.TryGetValue(key, out var value) ? value : throw new ArgumentException($"Missing required option --{key}");

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  LameRegression run    --run-dir DIR --name NAME --input WAV --options \"LAME OPTIONS\"");
        Console.WriteLine("                        --old OLD_LAME_EXE --new NEW_LAME_EXE [--iterations N]");
        Console.WriteLine("  LameRegression report --run-dir DIR [--threshold PERCENT]");
    }
}
