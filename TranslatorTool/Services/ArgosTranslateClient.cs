using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace TranslatorTool.Services;

public sealed class ArgosTranslateClient(SettingsService settingsService) : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly SemaphoreSlim _requestLock = new(1, 1);
    private Process? _serverProcess;

    public Task<ArgosTranslateResult> TranslateAsync(string text, CancellationToken cancellationToken)
    {
        return TranslateAsync(text, "en", "zh", cancellationToken);
    }

    public async Task<ArgosTranslateResult> TranslateAsync(
        string text,
        string fromCode,
        string toCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return ArgosTranslateResult.Success(string.Empty);
        }

        await _requestLock.WaitAsync(cancellationToken);
        try
        {
            var process = await EnsureServerAsync(cancellationToken);
            if (process is null)
            {
                return ArgosTranslateResult.Failure("Unable to start Argos Translate server.");
            }

            var requestJson = JsonSerializer.Serialize(new
            {
                text,
                from = fromCode,
                to = toCode,
                preserveLines = true
            }, JsonOptions);

            await process.StandardInput.WriteLineAsync(requestJson);
            await process.StandardInput.FlushAsync(cancellationToken);

            var output = await ReadLineWithTimeoutAsync(process, TimeSpan.FromSeconds(18), cancellationToken);
            if (string.IsNullOrWhiteSpace(output))
            {
                RestartServer();
                return ArgosTranslateResult.Failure("Argos Translate returned no output.");
            }

            return ParsePayload(output);
        }
        finally
        {
            _requestLock.Release();
        }
    }

    public void Dispose()
    {
        _requestLock.Dispose();
        StopServer();
    }

    private async Task<Process?> EnsureServerAsync(CancellationToken cancellationToken)
    {
        if (_serverProcess is { HasExited: false })
        {
            return _serverProcess;
        }

        StopServer();
        var settings = settingsService.Load();
        var pythonPath = string.IsNullOrWhiteSpace(settings.ArgosPythonPath) ? "python" : settings.ArgosPythonPath.Trim();
        var serverPath = Path.Combine(AppContext.BaseDirectory, "Scripts", "argos_translate_server.py");
        if (!File.Exists(serverPath))
        {
            DiagnosticsLog.Write($"Argos server script not found: {serverPath}");
            return null;
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = $"-u \"{serverPath}\"",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            },
            EnableRaisingEvents = true
        };
        process.StartInfo.Environment["PYTHONIOENCODING"] = "utf-8";
        process.StartInfo.Environment["PYTHONUTF8"] = "1";

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Write($"Unable to start Argos server: {ex.Message}");
            process.Dispose();
            return null;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var stderr = await process.StandardError.ReadToEndAsync();
                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    DiagnosticsLog.Write($"Argos server stderr: {stderr.Trim()}");
                }
            }
            catch
            {
                // Diagnostics only.
            }
        }, CancellationToken.None);

        var ready = await ReadLineWithTimeoutAsync(process, TimeSpan.FromSeconds(20), cancellationToken);
        if (string.IsNullOrWhiteSpace(ready) || !ready.Contains("\"ready\"", StringComparison.OrdinalIgnoreCase))
        {
            TryKill(process);
            process.Dispose();
            DiagnosticsLog.Write("Argos server did not become ready.");
            return null;
        }

        _serverProcess = process;
        return process;
    }

    private static async Task<string> ReadLineWithTimeoutAsync(Process process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var readTask = process.StandardOutput.ReadLineAsync(cancellationToken).AsTask();
        var timeoutTask = Task.Delay(timeout, cancellationToken);
        return await Task.WhenAny(readTask, timeoutTask) == readTask
            ? await readTask ?? string.Empty
            : string.Empty;
    }

    private static ArgosTranslateResult ParsePayload(string output)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<ArgosBridgePayload>(output, JsonOptions);
            if (payload is null)
            {
                return ArgosTranslateResult.Failure("Argos Translate returned invalid JSON.");
            }

            if (!payload.Ok)
            {
                var detail = string.IsNullOrWhiteSpace(payload.Detail) ? string.Empty : $" {payload.Detail}";
                return ArgosTranslateResult.Failure($"{payload.Error}{detail}".Trim());
            }

            return ArgosTranslateResult.Success(payload.TranslatedText ?? string.Empty);
        }
        catch (JsonException ex)
        {
            return ArgosTranslateResult.Failure($"Argos Translate returned invalid JSON: {ex.Message}");
        }
    }

    private void RestartServer()
    {
        StopServer();
    }

    private void StopServer()
    {
        if (_serverProcess is null)
        {
            return;
        }

        try
        {
            if (!_serverProcess.HasExited)
            {
                _serverProcess.StandardInput.WriteLine("{\"command\":\"shutdown\"}");
                if (!_serverProcess.WaitForExit(1000))
                {
                    TryKill(_serverProcess);
                }
            }
        }
        catch
        {
            TryKill(_serverProcess);
        }
        finally
        {
            _serverProcess.Dispose();
            _serverProcess = null;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }

    private sealed class ArgosBridgePayload
    {
        public bool Ok { get; set; }
        public string? TranslatedText { get; set; }
        public string? Error { get; set; }
        public string? Detail { get; set; }
    }
}

public sealed record ArgosTranslateResult(bool Ok, string TranslatedText, string Error)
{
    public static ArgosTranslateResult Success(string translatedText) => new(true, translatedText, string.Empty);
    public static ArgosTranslateResult Failure(string error) => new(false, string.Empty, error);
}
