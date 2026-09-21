using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;

namespace Moongate.Api.Tests.TestSupport.Processes;

internal sealed class ApiChildProcess : IAsyncDisposable
{
    private readonly Process _process;
    private readonly Task _stdout;
    private readonly Task<string> _stderr;
    private readonly Channel<string> _lines = Channel.CreateUnbounded<string>();
    private bool _disposed;

    private ApiChildProcess(Process process)
    {
        _process = process;
        _stdout = DrainOutputAsync();
        _stderr = process.StandardError.ReadToEndAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            if (!_process.HasExited)
            {
                try
                {
                    await SendAsync("STOP");
                    _process.StandardInput.Close();
                }
                catch (IOException) { }

                try
                {
                    await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (TimeoutException)
                {
                    if (!_process.HasExited)
                    {
                        _process.Kill(true);
                    }

                    await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                }
            }

            await Task.WhenAll(_stdout, _stderr);
        }
        finally
        {
            _process.Dispose();
        }
    }

    public async Task ExpectExitAsync()
    {
        await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));

        if (_process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Test host failed: {await _stderr}");
        }
    }

    public async Task<string> ReadLineAsync()
    {
        try
        {
            return await _lines.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15));
        }
        catch (ChannelClosedException)
        {
            throw new InvalidOperationException($"Test host exited: {await _stderr}");
        }
    }

    public async Task SendAsync(string command)
    {
        await _process.StandardInput.WriteLineAsync(command);
        await _process.StandardInput.FlushAsync();
    }

    public static async Task<ApiChildProcess> StartAsync(string role, object configuration)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "TestHost", "Moongate.Api.TestHost.dll"));
        start.ArgumentList.Add(role);
        var child = new ApiChildProcess(
            Process.Start(start) ?? throw new InvalidOperationException("Could not start test host.")
        );

        try
        {
            await child._process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(configuration));
            await child._process.StandardInput.FlushAsync();

            return child;
        }
        catch
        {
            await child.DisposeAsync();

            throw;
        }
    }

    private async Task DrainOutputAsync()
    {
        try
        {
            while (await _process.StandardOutput.ReadLineAsync() is { } line)
            {
                await _lines.Writer.WriteAsync(line);
            }
        }
        finally
        {
            _lines.Writer.TryComplete();
        }
    }
}
