using System.Text;
using static System.Net.WebUtility;

namespace LameRegression;

// Формирует HTML-отчёт о прогоне
public static class ReportWriter
{
    public static void Write(string path, List<ScenarioResult> results, double threshold, string logFile)
    {
        var first = results.FirstOrDefault(r => r.Error == null) ?? results[0];
        var html = new StringBuilder();

        html.Append($$"""
            <!DOCTYPE html>
            <html lang="ru">
            <head>
            <meta charset="utf-8">
            <title>Отчёт о тестировании LAME</title>
            <style>
              body { font-family: Arial, sans-serif; margin: 20px; }
              table { border-collapse: collapse; }
              th, td { border: 1px solid #999; padding: 3px 6px; }
            </style>
            </head>
            <body>
            <h1>Отчёт о тестировании LAME</h1>
            <p>Дата: {{DateTime.Now:dd.MM.yyyy HH:mm:ss}}</p>
            """);

        // ---- Условия тестирования ----
        html.Append("<h2>Условия тестирования</h2><table>");
        foreach (var (name, value) in EnvironmentInfo.Collect())
            html.Append($"<tr><th>{HtmlEncode(name)}</th><td>{HtmlEncode(value)}</td></tr>");
        html.Append($"<tr><th>Замеров на сценарий</th><td>{first.Iterations} (+1 прогревочный)</td></tr>");
        html.Append("</table>");

        html.Append("<h3>Версии LAME</h3><table><tr><th></th><th>Версия</th><th>Путь</th></tr>");
        foreach (var (role, v) in new[] { ("old", first.Old), ("new", first.New) })
            html.Append($"<tr><td>{role}</td><td>{HtmlEncode(v.Version)}</td><td>{HtmlEncode(v.LamePath)}</td></tr>");
        html.Append("</table>");

        html.Append("<h3>Входные файлы</h3><table><tr><th>Файл</th><th>Формат</th><th>Размер, байт</th></tr>");
        foreach (string input in results.Select(r => r.InputFile).Distinct())
            html.Append($"<tr><td>{HtmlEncode(input)}</td><td>{WavDescription(input)}</td>" +
                        $"<td>{(File.Exists(input) ? new FileInfo(input).Length : 0)}</td></tr>");
        html.Append("</table>");

        // ---- Результаты ----
        html.Append("""
            <h2>Результаты</h2>
            <table>
            <tr><th>Сценарий</th><th>Входной файл</th><th>Параметры</th>
                <th>old, мс<br>мин / сред / медиана</th><th>new, мс<br>мин / сред / медиана</th>
                <th>Разница (медиана)</th><th>CPU old / new, мс</th><th>× реального времени<br>old / new</th>
                <th>Размер old / new, байт</th><th>Файлы</th><th>Аудиоданные<br>(без LAME-тега)</th></tr>
            """);
        foreach (var r in results)
        {
            if (r.Error != null)
            {
                html.Append($"<tr><td>{HtmlEncode(r.Name)}</td><td>{HtmlEncode(Path.GetFileName(r.InputFile))}</td>" +
                            $"<td>{HtmlEncode(r.Options)}</td><td colspan=\"8\">ОШИБКА: {HtmlEncode(r.Error)}</td></tr>");
                continue;
            }

            string slower = r.SpeedDiffPercent > threshold ? " (замедление!)" : "";
            html.Append($"""
                <tr><td>{HtmlEncode(r.Name)}</td><td>{HtmlEncode(Path.GetFileName(r.InputFile))}</td>
                <td>{HtmlEncode(r.Options)}</td>
                <td>{Times(r.Old)}</td><td>{Times(r.New)}</td>
                <td>{r.SpeedDiffPercent:+0.0;-0.0;0.0}%{slower}</td>
                <td>{r.Old.MedianCpuMs:F0} / {r.New.MedianCpuMs:F0}</td>
                <td>{Realtime(r, r.Old):F1} / {Realtime(r, r.New):F1}</td>
                <td>{r.Old.OutputSize} / {r.New.OutputSize}</td>
                <td>{Verdict(r.FilesIdentical)}</td>
                <td>{Verdict(r.AudioIdentical)}</td></tr>
                """);
        }
        html.Append("</table>");

        var ok = results.Where(r => r.Error == null).ToList();
        html.Append($"<p>Итого: сценариев {results.Count}, с ошибками {results.Count - ok.Count}, " +
                    $"файлы различаются в {ok.Count(r => !r.FilesIdentical)}, " +
                    $"аудиоданные различаются в {ok.Count(r => !r.AudioIdentical)}, " +
                    $"замедление больше {threshold}% в {ok.Count(r => r.SpeedDiffPercent > threshold)}.</p>");

        // ---- Лог ----
        string log = File.Exists(logFile) ? File.ReadAllText(logFile) : "(лог не найден)";
        html.Append($"<h2>Лог выполнения</h2><pre>{HtmlEncode(log)}</pre></body></html>");

        File.WriteAllText(path, html.ToString(), Encoding.UTF8);
    }

    private static string Times(VersionResult v) => $"{v.MinMs:F0} / {v.AvgMs:F0} / {v.MedianMs:F0}";

    private static double Realtime(ScenarioResult r, VersionResult v) =>
        v.MedianMs > 0 ? r.AudioSeconds / (v.MedianMs / 1000) : 0;

    private static string Verdict(bool identical) => identical ? "одинаковые" : "различаются";

    private static string WavDescription(string path)
    {
        try { return HtmlEncode(WavInfo.Read(path).ToString()); }
        catch (Exception ex) { return HtmlEncode(ex.Message); }
    }
}
