using BetBuilder.Api.Grpc;
using BetBuilder.Application.Interfaces;
using BetBuilder.Infrastructure.Snapshots;
using Grpc.Core;

namespace BetBuilder.Api.GrpcServices;

public sealed class SnapshotIngestService : SnapshotIngest.SnapshotIngestBase
{
    private readonly IPricingSnapshotFactory _factory;
    private readonly IActiveSnapshotStore _store;
    private readonly ILogger<SnapshotIngestService> _logger;

    public SnapshotIngestService(
        IPricingSnapshotFactory factory,
        IActiveSnapshotStore store,
        ILogger<SnapshotIngestService> logger)
    {
        _factory = factory;
        _store = store;
        _logger = logger;
    }

    public override async Task StreamSnapshots(
        IAsyncStreamReader<BinarySnapshotMessage> requestStream,
        IServerStreamWriter<SnapshotAck> responseStream,
        ServerCallContext context)
    {
        _logger.LogInformation("gRPC snapshot stream opened");

        await foreach (var msg in requestStream.ReadAllAsync(context.CancellationToken))
        {
            var ack = await ProcessMessage(msg);
            await responseStream.WriteAsync(ack, context.CancellationToken);
        }

        _logger.LogInformation("gRPC snapshot stream closed");
    }

    private Task<SnapshotAck> ProcessMessage(BinarySnapshotMessage msg)
    {
        try
        {
            var content = new SnapshotBinaryContent
            {
                SnapshotId = msg.SnapshotId,
                Legs = msg.Legs.ToList(),
                PackedRows = msg.PackedRows.ToByteArray(),
                ScenarioCount = msg.ScenarioCount,
                EventId = string.IsNullOrEmpty(msg.EventId) ? null : msg.EventId,
                ModelVersion = string.IsNullOrEmpty(msg.ModelVersion) ? null : msg.ModelVersion
            };

            var snapshot = _factory.BuildFromBinary(content);
            _store.LoadSnapshot(snapshot);

            var activated = false;
            if (msg.Activate)
            {
                _store.SetActiveSnapshot(snapshot.SnapshotId);
                activated = true;
            }

            _logger.LogInformation(
                "gRPC: ingested snapshot {SnapshotId} ({Legs} legs, {Scenarios} scenarios, activated={Activated})",
                snapshot.SnapshotId, snapshot.LegCount, snapshot.ScenarioCount, activated);

            return Task.FromResult(new SnapshotAck
            {
                SnapshotId = snapshot.SnapshotId,
                LegCount = snapshot.LegCount,
                ScenarioCount = snapshot.ScenarioCount,
                Activated = activated
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "gRPC: failed to process snapshot {SnapshotId}", msg.SnapshotId);

            return Task.FromResult(new SnapshotAck
            {
                SnapshotId = msg.SnapshotId,
                Error = ex.Message
            });
        }
    }
}
