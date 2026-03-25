using Moongate.Server.Interfaces.Services.Files;
using Moongate.TemplateValidator.Data;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Moongate.TemplateValidator.Services;

public sealed class TemplateValidationRunner
{
    private readonly TemplateValidatorCompositionRoot _compositionRoot;

    public TemplateValidationRunner()
        : this(new TemplateValidatorCompositionRoot())
    {
    }

    public TemplateValidationRunner(TemplateValidatorCompositionRoot compositionRoot)
    {
        _compositionRoot = compositionRoot ?? throw new ArgumentNullException(nameof(compositionRoot));
    }

    public async Task<TemplateValidationResult> ValidateAsync(
        string rootDirectory,
        CancellationToken cancellationToken = default
    )
    {
        var previousLogger = Log.Logger;
        var sink = new ValidationLogSink();
        using var logger = new LoggerConfiguration()
                           .MinimumLevel
                           .Verbose()
                           .WriteTo
                           .Sink(sink)
                           .CreateLogger();

        Log.Logger = logger;

        try
        {
            var context = _compositionRoot.Create(rootDirectory);

            await LoadAsync(context.ContainersDataLoader, cancellationToken);
            await LoadAsync(context.ItemTemplateLoader, cancellationToken);
            await LoadAsync(context.MobileTemplateLoader, cancellationToken);
            await LoadAsync(context.LootTemplateLoader, cancellationToken);
            await LoadAsync(context.FactionTemplateLoader, cancellationToken);
            await LoadAsync(context.SellProfileTemplateLoader, cancellationToken);
            await LoadAsync(context.TemplateValidationLoader, cancellationToken);

            return new()
            {
                ExitCode = 0,
                ItemTemplateCount = context.ItemTemplateService.Count,
                MobileTemplateCount = context.MobileTemplateService.Count,
                Summary =
                    $"Template validation completed successfully. ItemTemplates={context.ItemTemplateService.Count}, MobileTemplates={context.MobileTemplateService.Count}."
            };
        }
        catch (Exception ex)
        {
            var errors = sink.GetErrors();

            if (errors.Count == 0)
            {
                errors.Add(ex.Message);
            }

            return new()
            {
                ExitCode = 1,
                Summary = $"Template validation failed: {ex.Message}",
                Errors = errors
            };
        }
        finally
        {
            Log.Logger = previousLogger;
        }
    }

    private static async Task LoadAsync(IFileLoader loader, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await loader.LoadAsync();
    }

    private sealed class ValidationLogSink : ILogEventSink
    {
        private readonly List<string> _errors = [];

        public void Emit(LogEvent logEvent)
        {
            ArgumentNullException.ThrowIfNull(logEvent);

            if (logEvent.Level < LogEventLevel.Error)
            {
                return;
            }

            _errors.Add(logEvent.RenderMessage());
        }

        public List<string> GetErrors()
            => [.._errors];
    }
}
