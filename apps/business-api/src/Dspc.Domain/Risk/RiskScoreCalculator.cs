using Dspc.Domain.Common;

namespace Dspc.Domain.Risk;

public static class RiskFactorCodes
{
    public const string EtaDeviation = "ETA_DEVIATION";
    public const string Criticality = "CRITICALITY";
    public const string NoAlternative = "NO_ALTERNATIVE";
    public const string DocCompleteness = "DOC_COMPLETENESS";
    public const string SupplierReliability = "SUPPLIER_RELIABILITY";
    public const string Coverage = "COVERAGE";
    public const string LogisticsEvents = "LOGISTICS_EVENTS";
}

/// <summary>Configurable weights; must sum to 1.0.</summary>
public sealed class RiskWeights
{
    public double EtaDeviation { get; set; } = 0.35;
    public double Criticality { get; set; } = 0.15;
    public double NoAlternative { get; set; } = 0.10;
    public double DocCompleteness { get; set; } = 0.15;
    public double SupplierReliability { get; set; } = 0.10;
    public double Coverage { get; set; } = 0.10;
    public double LogisticsEvents { get; set; } = 0.05;

    public double Sum => EtaDeviation + Criticality + NoAlternative + DocCompleteness + SupplierReliability + Coverage + LogisticsEvents;
}

public sealed record RiskInput(
    int DaysLate,
    int Criticality,
    bool HasAlternativeSupplier,
    int RequiredDocuments,
    int AcceptedDocuments,
    double SupplierOtifPercent,
    decimal OpenDemand,
    decimal FreeOnHand,
    IReadOnlyList<EventSeverity> ActiveEvents);

public sealed record RiskFactor(string Code, double Raw, double Weight, double Contribution);

public sealed record RiskResult(int Score, RiskCategory Category, IReadOnlyList<RiskFactor> Factors)
{
    public IReadOnlyList<RiskFactor> TopFactors => Factors.OrderByDescending(f => f.Contribution).ThenBy(f => f.Code).Take(3).ToList();
}

public static class RiskScoreCalculator
{
    public static RiskCategory Categorize(int score) => score switch
    {
        < 25 => RiskCategory.Low,
        < 50 => RiskCategory.Medium,
        < 75 => RiskCategory.High,
        _ => RiskCategory.Critical
    };

    public static RiskResult Calculate(RiskInput input, RiskWeights weights)
    {
        double eta = input.DaysLate <= 0 ? 0 : Math.Min(100, input.DaysLate * 12);
        double crit = Math.Clamp((input.Criticality - 1) * 25, 0, 100);
        double noAlt = input.HasAlternativeSupplier ? 0 : 100;
        double docs = input.RequiredDocuments <= 0
            ? 0
            : (1.0 - Math.Clamp((double)input.AcceptedDocuments / input.RequiredDocuments, 0, 1)) * 100;
        double reliability = Math.Clamp(100 - input.SupplierOtifPercent, 0, 100);
        double coverage = 0;
        if (input.OpenDemand > 0)
        {
            var shortage = Math.Max(0, input.OpenDemand - Math.Max(0, input.FreeOnHand));
            coverage = (double)(shortage / input.OpenDemand) * 100;
        }
        double events = Math.Min(100, input.ActiveEvents.Sum(s => s switch
        {
            EventSeverity.LOW => 25,
            EventSeverity.MEDIUM => 50,
            _ => 100
        }));

        var factors = new List<RiskFactor>
        {
            Factor(RiskFactorCodes.EtaDeviation, eta, weights.EtaDeviation),
            Factor(RiskFactorCodes.Criticality, crit, weights.Criticality),
            Factor(RiskFactorCodes.NoAlternative, noAlt, weights.NoAlternative),
            Factor(RiskFactorCodes.DocCompleteness, docs, weights.DocCompleteness),
            Factor(RiskFactorCodes.SupplierReliability, reliability, weights.SupplierReliability),
            Factor(RiskFactorCodes.Coverage, coverage, weights.Coverage),
            Factor(RiskFactorCodes.LogisticsEvents, events, weights.LogisticsEvents),
        };
        var score = (int)Math.Round(factors.Sum(f => f.Contribution), MidpointRounding.AwayFromZero);
        score = Math.Clamp(score, 0, 100);
        return new RiskResult(score, Categorize(score), factors);
    }

    private static RiskFactor Factor(string code, double raw, double weight)
        => new(code, Math.Round(raw, 2), weight, Math.Round(raw * weight, 2));
}
