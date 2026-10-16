namespace LameRegression;

// Логика выполнения сценария: прогрев, замеры обеих версий LAME, сравнение mp3.
// Результат возвращается в виде ScenarioResult.
public static class ScenarioRunner
{
    public static ScenarioResult Run(string name, string input, string options,
        string oldLame, string newLame, int iterations, string outputDir)
    {
        var result = new ScenarioResult
        {
            Name = name,
            InputFile = input,
            Options = options,
            Iterations = iterations,
        };

        try
        {
            Directory.CreateDirectory(outputDir);
            string[] optionArgs = options.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var wav = WavInfo.Read(input);
            result.AudioSeconds = wav.DurationSeconds;
            Logger.Info($"[{name}] Input: {input} ({wav})");
            Logger.Info($"[{name}] Options: {options}");

            result.Old = Prepare(oldLame, Path.Combine(outputDir, "old.mp3"));
            result.New = Prepare(newLame, Path.Combine(outputDir, "new.mp3"));
            Logger.Info($"[{name}] Old: {result.Old.Version} ({oldLame})");
            Logger.Info($"[{name}] New: {result.New.Version} ({newLame})");

            // Прогревочный запуск (не учитывается): дисковый кэш, загрузка exe и dll
            Logger.Info($"[{name}] Warm-up run");
            LameRunner.Encode(oldLame, optionArgs, input, result.Old.OutputFile);
            LameRunner.Encode(newLame, optionArgs, input, result.New.OutputFile);

            // Версии запускаются поочерёдно, чтобы фоновая нагрузка влияла на обе одинаково
            for (int i = 1; i <= iterations; i++)
            {
                Measure(result.Old, optionArgs, input, name, i);
                Measure(result.New, optionArgs, input, name, i);
            }

            SaveFileInfo(result.Old);
            SaveFileInfo(result.New);
            result.FilesIdentical = result.Old.Sha256 == result.New.Sha256;
            result.AudioIdentical = result.Old.AudioSha256 == result.New.AudioSha256;

            Logger.Info($"[{name}] Median time: old {result.Old.MedianMs:F0} ms, new {result.New.MedianMs:F0} ms " +
                        $"({result.SpeedDiffPercent:+0.0;-0.0}%)");
            Logger.Info($"[{name}] Files identical: {result.FilesIdentical}, audio frames identical: {result.AudioIdentical}");
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
            Logger.Error($"[{name}] {ex.Message}");
        }

        return result;
    }

    private static VersionResult Prepare(string lameExe, string outputFile)
    {
        if (!File.Exists(lameExe))
            throw new FileNotFoundException($"LAME not found: {lameExe}");
        return new VersionResult
        {
            LamePath = lameExe,
            Version = LameRunner.GetVersion(lameExe),
            OutputFile = outputFile,
        };
    }

    private static void Measure(VersionResult v, string[] options, string input, string name, int iteration)
    {
        var timing = LameRunner.Encode(v.LamePath, options, input, v.OutputFile);
        v.TimesMs.Add(Math.Round(timing.ElapsedMs, 1));
        v.CpuMs.Add(Math.Round(timing.CpuMs, 1));
        Logger.Info($"[{name}] #{iteration} {v.Version}: {timing.ElapsedMs:F0} ms (CPU {timing.CpuMs:F0} ms)");
    }

    private static void SaveFileInfo(VersionResult v)
    {
        v.Sha256 = Mp3Compare.Sha256(v.OutputFile);
        v.AudioSha256 = Mp3Compare.AudioSha256(v.OutputFile);
        v.OutputSize = new FileInfo(v.OutputFile).Length;
    }
}
