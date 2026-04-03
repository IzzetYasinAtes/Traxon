using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Traxon.CryptoTrader.Application.Abstractions;

namespace Traxon.CryptoTrader.Infrastructure.Services;

public sealed class PythonSignerHostedService : IHostedService, IDisposable
{
    private readonly ISecureSettingService _settings;
    private readonly ILogger<PythonSignerHostedService> _logger;
    private Process? _process;

    public PythonSignerHostedService(
        ISecureSettingService settings,
        ILogger<PythonSignerHostedService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[PythonSigner] Starting signing service...");

        // Read credentials from secure settings
        var privateKey = await _settings.GetAsync("Polymarket:PrivateKey");
        var walletAddress = await _settings.GetAsync("Polymarket:WalletAddress") ?? "";
        var sigTypeStr = await _settings.GetAsync("Polymarket:SignatureType") ?? "0";

        if (string.IsNullOrEmpty(privateKey))
        {
            _logger.LogWarning("[PythonSigner] No POLY_PRIVATE_KEY configured. Signing service will NOT start.");
            return;
        }

        // Find scripts/polymarket-signer/ directory
        var scriptDir = FindScriptDirectory();
        if (scriptDir is null)
        {
            _logger.LogError("[PythonSigner] Could not find scripts/polymarket-signer/ directory");
            return;
        }

        var scriptPath = Path.Combine(scriptDir, "signing_service.py");
        if (!File.Exists(scriptPath))
        {
            _logger.LogError("[PythonSigner] signing_service.py not found at {Path}", scriptPath);
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = FindPythonExecutable(),
            Arguments = "signing_service.py",
            WorkingDirectory = scriptDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        psi.Environment["POLY_PRIVATE_KEY"] = privateKey;
        psi.Environment["POLY_FUNDER_ADDRESS"] = walletAddress;
        psi.Environment["POLY_SIGNATURE_TYPE"] = sigTypeStr;
        psi.Environment["SIGNER_PORT"] = "5099";

        try
        {
            _process = Process.Start(psi);

            if (_process is null)
            {
                _logger.LogError("[PythonSigner] Failed to start python process");
                return;
            }

            // Forward stdout/stderr to logger
            _process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    _logger.LogDebug("[PythonSigner:stdout] {Line}", e.Data);
            };
            _process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    _logger.LogDebug("[PythonSigner:stderr] {Line}", e.Data);
            };
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            _logger.LogInformation("[PythonSigner] Python process started (PID: {Pid})", _process.Id);

            // Health check loop
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var healthy = false;
            for (var i = 0; i < 10; i++)
            {
                await Task.Delay(1000, cancellationToken);

                if (_process.HasExited)
                {
                    _logger.LogError("[PythonSigner] Python process exited prematurely (exit code: {Code})",
                        _process.ExitCode);
                    return;
                }

                try
                {
                    var response = await httpClient.GetAsync("http://127.0.0.1:5099/health", cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        healthy = true;
                        break;
                    }
                }
                catch
                {
                    // Not ready yet
                }
            }

            if (healthy)
                _logger.LogInformation("[PythonSigner] Signing service is healthy and ready on port 5099");
            else
                _logger.LogWarning("[PythonSigner] Signing service did not become healthy within timeout");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PythonSigner] Failed to start signing service");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_process is null || _process.HasExited)
            return Task.CompletedTask;

        _logger.LogInformation("[PythonSigner] Stopping signing service (PID: {Pid})...", _process.Id);

        try
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit(5000);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PythonSigner] Error while stopping python process");
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_process is not null)
        {
            if (!_process.HasExited)
            {
                try { _process.Kill(entireProcessTree: true); }
                catch { /* best effort */ }
            }
            _process.Dispose();
            _process = null;
        }
    }

    private static string FindPythonExecutable()
    {
        // Common Python install locations on Windows
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Python", "Python312", "python.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Python", "Python313", "python.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Python", "Python311", "python.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Python312", "python.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Python313", "python.exe"),
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        // Fallback to PATH
        return "python";
    }

    private static string? FindScriptDirectory()
    {
        // Try to find scripts/polymarket-signer relative to common locations
        var candidates = new[]
        {
            // Relative to current directory (project root when running from VS/dotnet run)
            Path.Combine(Environment.CurrentDirectory, "scripts", "polymarket-signer"),
            // Walk up from current directory
            Path.Combine(Environment.CurrentDirectory, "..", "scripts", "polymarket-signer"),
            Path.Combine(Environment.CurrentDirectory, "..", "..", "scripts", "polymarket-signer"),
            Path.Combine(Environment.CurrentDirectory, "..", "..", "..", "scripts", "polymarket-signer"),
            Path.Combine(Environment.CurrentDirectory, "..", "..", "..", "..", "scripts", "polymarket-signer"),
            Path.Combine(Environment.CurrentDirectory, "..", "..", "..", "..", "..", "scripts", "polymarket-signer"),
            // Relative to assembly location
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "scripts", "polymarket-signer"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "scripts", "polymarket-signer"),
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (Directory.Exists(fullPath) && File.Exists(Path.Combine(fullPath, "signing_service.py")))
                return fullPath;
        }

        return null;
    }
}
