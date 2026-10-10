using System.Diagnostics;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Moongate.Admin.Contracts.V1;
using Moongate.Server.Admin.Internal;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Admin.Services.Grpc;

internal sealed class AdminOperationsGrpcService : AdminOperations.AdminOperationsBase
{
    private const int MaximumBroadcastLength = 200;

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
            request.Text.Contains('\0'))
        {
            throw new RpcException(new(StatusCode.InvalidArgument, "Invalid broadcast text."));
        }

        try
        {
            return new() { Recipients = (uint)await _broadcast.BroadcastAsync(request.Text, context.CancellationToken) };
        }
        catch (ArgumentException)
        {
            // The text does not fit the transport once compressed.
            throw new RpcException(new(StatusCode.InvalidArgument, "The broadcast text is too long for the transport."));
        }
    }

    public override async Task<SaveWorldResponse> SaveWorld(Empty request, ServerCallContext context)
    {
        var start = Stopwatch.GetTimestamp();
        await _saves.SaveAsync(context.CancellationToken);

        return new() { DurationMs = (uint)Math.Min(uint.MaxValue, Stopwatch.GetElapsedTime(start).TotalMilliseconds) };
    }

    public override async Task<CreateSqlBackupResponse> CreateSqlBackup(Empty request, ServerCallContext context)
    {
        var result = await _backups.BackupAsync(context.CancellationToken);
        var response = new CreateSqlBackupResponse { AlreadyRunning = result.AlreadyRunning };

        // Only the name of each file: where the server keeps its backups is not for the client.
        response.Files.AddRange(
            result.Files.Select(file => new BackupFile
                {
                    Database = file.Database, FileName = Path.GetFileName(file.Path), SizeBytes = (ulong)Math.Max(0, file.Size)
                }
            )
        );
        response.Failures.AddRange(
            result.Failures.Select(failure => new BackupFailure { Database = failure.Database, Reason = failure.Reason })
        );

        return response;
    }
}
