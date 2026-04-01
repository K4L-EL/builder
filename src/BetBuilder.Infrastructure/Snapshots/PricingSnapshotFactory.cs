using BetBuilder.Domain;
using BetBuilder.Infrastructure.Binary;
using BetBuilder.Infrastructure.Csv;
using Microsoft.Extensions.Logging;

namespace BetBuilder.Infrastructure.Snapshots;

public sealed class PricingSnapshotFactory : IPricingSnapshotFactory
{
    private readonly ILogger<PricingSnapshotFactory> _logger;

    public PricingSnapshotFactory(ILogger<PricingSnapshotFactory> logger)
    {
        _logger = logger;
    }

    public PricingSnapshot Build(SnapshotFileGroup fileGroup)
    {
        _logger.LogInformation("Building snapshot {SnapshotId} from files...", fileGroup.SnapshotId);

        var outcomeData = CsvOutcomeMatrixReader.Read(fileGroup.OutcomeMatrixPath);
        var legProbData = CsvLegProbReader.Read(fileGroup.LegProbsPath);
        var correlationData = CsvCorrelationMatrixReader.Read(fileGroup.CorrelationMatrixPath);

        return Assemble(fileGroup.SnapshotId, "default", "1.0", outcomeData, legProbData, correlationData);
    }

    public PricingSnapshot BuildFromContent(SnapshotCsvContent content)
    {
        _logger.LogInformation("Building snapshot {SnapshotId} from uploaded content...", content.SnapshotId);

        var outcomeData = CsvOutcomeMatrixReader.ParseFromContent(content.OutcomeMatrixCsv);

        var legProbData = !string.IsNullOrWhiteSpace(content.LegProbsCsv)
            ? CsvLegProbReader.ParseFromContent(content.LegProbsCsv)
            : CsvLegProbReader.DeriveFromOutcomeMatrix(outcomeData);

        var correlationData = !string.IsNullOrWhiteSpace(content.CorrelationMatrixCsv)
            ? CsvCorrelationMatrixReader.ParseFromContent(content.CorrelationMatrixCsv)
            : CsvCorrelationMatrixReader.Empty(outcomeData.Legs);

        return Assemble(
            content.SnapshotId,
            content.EventId ?? "default",
            content.ModelVersion ?? "1.0",
            outcomeData, legProbData, correlationData);
    }

    public PricingSnapshot BuildFromBinary(SnapshotBinaryContent content)
    {
        _logger.LogInformation("Building snapshot {SnapshotId} from binary ({Scenarios} scenarios, {Legs} legs)...",
            content.SnapshotId, content.ScenarioCount, content.Legs.Count);

        var outcomeData = BinaryMatrixCodec.Unpack(content.Legs, content.PackedRows, content.ScenarioCount);
        var legProbData = CsvLegProbReader.DeriveFromOutcomeMatrix(outcomeData);
        var correlationData = CsvCorrelationMatrixReader.Empty(outcomeData.Legs);

        return Assemble(
            content.SnapshotId,
            content.EventId ?? "default",
            content.ModelVersion ?? "1.0",
            outcomeData, legProbData, correlationData);
    }

    private PricingSnapshot Assemble(
        string snapshotId, string eventId, string modelVersion,
        OutcomeMatrixData outcomeData, LegProbData legProbData, CorrelationMatrixData correlationData)
    {
        var legs = outcomeData.Legs;
        var legIndexMap = new Dictionary<string, int>(legs.Count);
        for (var i = 0; i < legs.Count; i++)
            legIndexMap[legs[i]] = i;

        ValidateConsistency(snapshotId, legs, legProbData, correlationData);

        var probabilities = CsvLegProbReader.AlignToIndex(legProbData, legs, legIndexMap);
        var correlationMatrix = CsvCorrelationMatrixReader.AlignToIndex(correlationData, legs, legIndexMap);

        var snapshot = new PricingSnapshot
        {
            SnapshotId = snapshotId,
            EventId = eventId,
            ModelVersion = modelVersion,
            GeneratedAtUtc = DateTime.UtcNow,
            Legs = legs,
            LegIndexMap = legIndexMap,
            LegProbabilities = probabilities,
            CorrelationMatrix = correlationMatrix,
            OutcomeMatrix = outcomeData.Rows,
            UnavailableLegs = outcomeData.UnavailableLegs
        };

        _logger.LogInformation(
            "Snapshot {SnapshotId} built: {LegCount} legs, {ScenarioCount} scenarios, {UnavailableCount} unavailable",
            snapshot.SnapshotId, snapshot.LegCount, snapshot.ScenarioCount, snapshot.UnavailableLegs.Count);

        return snapshot;
    }

    private void ValidateConsistency(
        string snapshotId,
        IReadOnlyList<string> outcomeLegs,
        LegProbData legProbData,
        CorrelationMatrixData correlationData)
    {
        var outcomeSet = new HashSet<string>(outcomeLegs);
        var probLegs = legProbData.Probabilities.Keys;
        var corrLegs = new HashSet<string>(correlationData.Legs);

        var missingInProbs = outcomeSet.Except(probLegs).ToList();
        if (missingInProbs.Count > 0)
        {
            _logger.LogWarning(
                "Snapshot {SnapshotId}: {Count} legs in outcome matrix missing from leg_probs: {Legs}",
                snapshotId, missingInProbs.Count, string.Join(", ", missingInProbs));
        }

        var missingInCorr = outcomeSet.Except(corrLegs).ToList();
        if (missingInCorr.Count > 0)
        {
            _logger.LogWarning(
                "Snapshot {SnapshotId}: {Count} legs in outcome matrix missing from correlation_matrix: {Legs}",
                snapshotId, missingInCorr.Count, string.Join(", ", missingInCorr));
        }
    }
}
