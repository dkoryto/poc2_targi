using Dspc.Domain.Common;
using Dspc.Domain.Risk;
using FluentAssertions;

namespace Dspc.Domain.Tests;

public class RiskScoreTests
{
    private static readonly RiskWeights Weights = new();

    // Worked example from docs/architecture/risk-model.md — PO-2026-0007/1 (ACT-40)
    private static RiskInput Act40(int daysLate) => new(daysLate, Criticality: 5, HasAlternativeSupplier: false, RequiredDocuments: 2, AcceptedDocuments: 1, SupplierOtifPercent: 88, OpenDemand: 18, FreeOnHand: 0, ActiveEvents: []);

    [Fact]
    public void Weights_sum_to_one() => Weights.Sum.Should().BeApproximately(1.0, 1e-9);

    [Fact]
    public void Act40_before_delay_is_44_medium()
    {
        var r = RiskScoreCalculator.Calculate(Act40(-1), Weights);
        r.Score.Should().Be(44);
        r.Category.Should().Be(RiskCategory.Medium);
        r.TopFactors.Select(f => f.Code).Should().ContainInOrder(RiskFactorCodes.Criticality, RiskFactorCodes.Coverage, RiskFactorCodes.NoAlternative);
    }

    [Fact]
    public void Act40_after_ten_days_delay_is_79_critical()
    {
        var r = RiskScoreCalculator.Calculate(Act40(9), Weights);
        r.Score.Should().Be(79);
        r.Category.Should().Be(RiskCategory.Critical);
        r.TopFactors.First().Code.Should().Be(RiskFactorCodes.EtaDeviation);
        r.Factors.Single(f => f.Code == RiskFactorCodes.EtaDeviation).Raw.Should().Be(100); // capped at 100 (9 × 12 = 108)
    }

    [Theory]
    [InlineData(0, RiskCategory.Low)]
    [InlineData(24, RiskCategory.Low)]
    [InlineData(25, RiskCategory.Medium)]
    [InlineData(49, RiskCategory.Medium)]
    [InlineData(50, RiskCategory.High)]
    [InlineData(74, RiskCategory.High)]
    [InlineData(75, RiskCategory.Critical)]
    [InlineData(100, RiskCategory.Critical)]
    public void Category_boundaries(int score, RiskCategory expected) => RiskScoreCalculator.Categorize(score).Should().Be(expected);

    [Fact]
    public void Logistics_events_are_capped_and_weighted()
    {
        var input = new RiskInput(0, 1, true, 0, 0, 100, 0, 0, [EventSeverity.HIGH, EventSeverity.HIGH]);
        var r = RiskScoreCalculator.Calculate(input, Weights);
        r.Factors.Single(f => f.Code == RiskFactorCodes.LogisticsEvents).Raw.Should().Be(100);
        r.Score.Should().Be(5);
    }

    [Fact]
    public void Score_is_clamped_between_0_and_100()
    {
        var worst = new RiskInput(30, 5, false, 2, 0, 0, 100, 0, [EventSeverity.HIGH]);
        RiskScoreCalculator.Calculate(worst, Weights).Score.Should().Be(100);
        var best = new RiskInput(-5, 1, true, 0, 0, 100, 0, 100, []);
        RiskScoreCalculator.Calculate(best, Weights).Score.Should().Be(0);
    }

    [Fact]
    public void Rejected_document_counts_as_missing()
    {
        var r = RiskScoreCalculator.Calculate(new RiskInput(0, 1, true, 2, 0, 100, 0, 10, []), Weights);
        r.Factors.Single(f => f.Code == RiskFactorCodes.DocCompleteness).Raw.Should().Be(100);
    }
}
