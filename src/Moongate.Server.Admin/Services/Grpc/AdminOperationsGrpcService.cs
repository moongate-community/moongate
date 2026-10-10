using System.Diagnostics;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Moongate.Admin.Contracts.V1;
using Moongate.Server.Admin.Internal;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Interfaces;
using Serilog;

namespace Moongate.Server.Admin.Services.Grpc;

internal sealed class AdminOperationsGrpcService : AdminOperations.AdminOperationsBase
{
    private const int MaximumBroadcastLength = 200;
    private const string FailureText = "The backup failed; see the server log.";

    // The saves and the backups are heavy: a few at a time, so they cannot fill the admission gate of the host.
    private const int MaximumHeavyCalls = 2;
    private static readonly SemaphoreSlim HeavyCalls = new(MaximumHeavyCalls, MaximumHeavyCalls);

    private readonly IBroadcastService _broadcast;
    private readonly IWorldSaveService _saves;
    private readonly ISqlBackupService _backups;

    public AdminOperationsGrpcService(AdminWorldServices world)
    {
        _broadcast = world.Broadcast;
        _saves = world.Saves;
        _backups = world.Backups;
    }

    public override async Task<BroadcastResponse> Broadcast(BroadcastRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.Text) ||
            request.Text.Length > MaximumBroadcastLength ||
            request.Text.Any(char.IsControl))
        {
            throw new RpcException(new(StatusCode.InvalidArgument, "Invalid broadcast text."));
        }

        try
        {
            return new() { Recipients = (uint)await _broadcast.BroadcastAsync(request.Text, context.CancellationToken) };
        }
        catch (ArgumentException exception) when (exception.ParamName == "text")
        {
            // The text does not fit the transport once compressed.
            throw new RpcException(new(StatusCode.InvalidArgument, "The broadcast text is too long for the transport."));
        }
    }

    public override async Task<SaveWorldResponse> SaveWorld(Empty request, ServerCallContext context)
    {
        using var heavy = EnterHeavyCall();
        var start = Stopwatch.GetTimestamp();

        try
        {
            // A save already running is the one this call waits for: the changes just before the call may not be in it.
            // The save goes on if the client leaves.
            await _saves.SaveAsync(context.CancellationToken);
        }
        catch (InvalidOperationException)
        {
            throw new RpcException(new(StatusCode.Unavailable, "World saving is not active or the server is stopping."));
        }

        return new() { DurationMs = (uint)Math.Min(uint.MaxValue, Stopwatch.GetElapsedTime(start).TotalMilliseconds) };
    }

    public override async Task<CreateSqlBackupResponse> CreateSqlBackup(Empty request, ServerCallContext context)
    {
        using var heavy = EnterHeavyCall();

        // The export is not cut in the middle of a file when the client leaves or the deadline ends: the call stops
        // waiting, the backup goes on.
        var running = _backups.BackupAsync();
        _ = ObserveAsync(running);
        var result = await running.WaitAsync(context.CancellationToken);
        var response = new CreateSqlBackupResponse { AlreadyRunning = result.AlreadyRunning };

        // Only the name of each file: where the server keeps its backups is not for the client.
        response.Files.AddRange(
            result.Files.Select(file => new BackupFile
                {
                    Database = file.Database, FileName = Path.GetFileName(file.Path), SizeBytes = (ulong)Math.Max(0, file.Size)
                }
            )
        );

        // The reason of a failure can carry paths or details of the database: the client gets a fixed text, the log the rest.
        foreach (var failure in result.Failures)
        {
            Log.ForContext<AdminOperationsGrpcService>()
                .Warning("SQL backup of {Database} failed: {Reason}", failure.Database, failure.Reason);
            response.Failures.Add(new BackupFailure { Database = failure.Database, Reason = FailureText });
        }

        return response;
    }

    private static IDisposable EnterHeavyCall()
    {
        if (!HeavyCalls.Wait(0))
        {
            throw new RpcException(new(StatusCode.ResourceExhausted, "A save or a backup is already being asked for."));
        }

        return new Release();
    }

    private static async Task ObserveAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception exception)
        {
            Log.ForContext<AdminOperationsGrpcService>().Warning(exception, "A SQL backup of the administration failed");
        }
    }

    private sealed class Release : IDisposable
    {
        public void Dispose()
        {
            HeavyCalls.Release();
        }
    }
}
