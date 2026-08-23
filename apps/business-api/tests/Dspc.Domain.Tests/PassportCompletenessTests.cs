using Dspc.Domain.Common;
using Dspc.Domain.Quality;
using FluentAssertions;

namespace Dspc.Domain.Tests;

/// <summary>Rules of template DQP-01 — the gate in front of PDF generation.</summary>
public class PassportCompletenessTests
{
    private static PassportComponentFacts Component(
        string part = "HTS-22", string lot = "HTS-22-2608", MaterialLotStatus status = MaterialLotStatus.Accepted,
        string? sha = "a1b2c3", string? country = "PL", string supplier = "SUP-01") =>
        new(part, $"{part} name", lot, "H-2608", supplier, "Nordstal", country, status, sha, "MC-2608", true);

    private static PassportFacts Complete(params PassportComponentFacts[] components) => new(
        "PMV-2026-0007", "P-MOB-03", "Pojazd chronionej mobilności", "WO-2026-011", "A",
        PassportStatus.Approved, "quality", new DateTime(2026, 8, 20, 9, 0, 0, DateTimeKind.Utc),
        components.Length == 0 ? [Component()] : components,
        [new PassportInspectionFacts("QI-2026-0501", InspectionResult.Passed, new DateTime(2026, 8, 19, 20, 0, 0, DateTimeKind.Utc), "quality", null)],
        []);

    [Fact]
    public void Complete_passport_satisfies_every_requirement()
    {
        var result = PassportCompletenessEvaluator.Evaluate(Complete());

        result.Complete.Should().BeTrue();
        result.Missing.Should().BeEmpty();
        result.Requirements.Should().HaveCount(PassportCompletenessEvaluator.Template.Count);
        result.Requirements.Should().OnlyContain(r => r.Satisfied);
    }

    [Fact]
    public void Requirements_keep_template_order_and_mandatory_flags()
    {
        var result = PassportCompletenessEvaluator.Evaluate(Complete());

        result.Requirements.Select(r => r.Code).Should().Equal(PassportCompletenessEvaluator.Template.Select(t => t.Code));
        result.Requirements.Single(r => r.Code == PassportCompletenessEvaluator.Requirements.Deviations).Mandatory.Should().BeFalse();
    }

    [Fact]
    public void Missing_product_data_blocks_generation()
    {
        var facts = Complete() with { ProductCode = null };

        var result = PassportCompletenessEvaluator.Evaluate(facts);

        result.Complete.Should().BeFalse();
        result.Missing.Select(m => m.Code).Should().Contain(PassportCompletenessEvaluator.Requirements.ProductData);
    }

    [Fact]
    public void Missing_order_reference_and_bom_version_are_both_reported()
    {
        var result = PassportCompletenessEvaluator.Evaluate(Complete() with { OrderCode = null, BomVersion = "  " });

        result.Missing.Select(m => m.Code).Should().Contain([
            PassportCompletenessEvaluator.Requirements.OrderRef,
            PassportCompletenessEvaluator.Requirements.BomVersion]);
    }

    [Fact]
    public void Serial_without_consumed_lots_misses_every_component_requirement()
    {
        var facts = Complete() with { Components = [] };

        var result = PassportCompletenessEvaluator.Evaluate(facts);

        result.Complete.Should().BeFalse();
        result.Missing.Select(m => m.Code).Should().Contain([
            PassportCompletenessEvaluator.Requirements.KeyComponentLots,
            PassportCompletenessEvaluator.Requirements.SupplierOrigin,
            PassportCompletenessEvaluator.Requirements.QcStatus,
            PassportCompletenessEvaluator.Requirements.CertificatesWithHash]);
    }

    [Fact]
    public void Component_without_certificate_hash_is_reported_with_the_lot_number()
    {
        var facts = Complete(Component(), Component(part: "ACT-40", lot: "ACT-40-0371", sha: null));

        var result = PassportCompletenessEvaluator.Evaluate(facts);

        result.Complete.Should().BeFalse();
        var missing = result.Missing.Single(m => m.Code == PassportCompletenessEvaluator.Requirements.CertificatesWithHash);
        missing.LabelKey.Should().Be("passports.missing.CERTIFICATES_WITH_HASH");
        missing.Params["lots"].Should().Be("ACT-40-0371");
    }

    [Theory]
    [InlineData(MaterialLotStatus.Blocked)]
    [InlineData(MaterialLotStatus.Recalled)]
    [InlineData(MaterialLotStatus.AwaitingInspection)]
    public void Lot_that_is_not_released_fails_the_qc_requirement(MaterialLotStatus status)
    {
        var result = PassportCompletenessEvaluator.Evaluate(Complete(Component(status: status)));

        result.Complete.Should().BeFalse();
        result.Missing.Select(m => m.Code).Should().Contain(PassportCompletenessEvaluator.Requirements.QcStatus);
    }

    [Fact]
    public void Conditionally_released_lot_still_passes_qc()
    {
        var result = PassportCompletenessEvaluator.Evaluate(Complete(Component(status: MaterialLotStatus.ConditionallyReleased)));

        result.Complete.Should().BeTrue();
    }

    [Fact]
    public void Unknown_country_of_origin_fails_the_origin_requirement()
    {
        var result = PassportCompletenessEvaluator.Evaluate(Complete(Component(country: null)));

        result.Missing.Select(m => m.Code).Should().Contain(PassportCompletenessEvaluator.Requirements.SupplierOrigin);
    }

    [Fact]
    public void Missing_final_inspection_blocks_generation()
    {
        var result = PassportCompletenessEvaluator.Evaluate(Complete() with { Inspections = [] });

        result.Complete.Should().BeFalse();
        result.Missing.Select(m => m.Code).Should().Contain(PassportCompletenessEvaluator.Requirements.InspectionResults);
    }

    [Fact]
    public void Failed_inspection_blocks_generation_even_when_a_passed_one_exists()
    {
        var facts = Complete() with
        {
            Inspections =
            [
                new PassportInspectionFacts("QI-1", InspectionResult.Passed, DateTime.UnixEpoch, "quality", null),
                new PassportInspectionFacts("QI-2", InspectionResult.Failed, DateTime.UnixEpoch, "quality", "wyciek")
            ]
        };

        var result = PassportCompletenessEvaluator.Evaluate(facts);

        result.Complete.Should().BeFalse();
        result.Missing.Select(m => m.Code).Should().Contain(PassportCompletenessEvaluator.Requirements.InspectionResults);
    }

    [Fact]
    public void Unapproved_deviation_does_not_block_generation_but_is_flagged_unsatisfied()
    {
        var facts = Complete() with { Deviations = [new PassportDeviationFacts("DEV-1", "Zamiennik uszczelnienia", null, null)] };

        var result = PassportCompletenessEvaluator.Evaluate(facts);

        result.Complete.Should().BeTrue("the deviations row is optional in DQP-01");
        result.Requirements.Single(r => r.Code == PassportCompletenessEvaluator.Requirements.Deviations).Satisfied.Should().BeFalse();
    }

    [Fact]
    public void Draft_passport_without_approval_is_incomplete()
    {
        var result = PassportCompletenessEvaluator.Evaluate(Complete() with { Status = PassportStatus.Draft, ApprovedBy = null, ApprovedAt = null });

        result.Complete.Should().BeFalse();
        result.Missing.Select(m => m.Code).Should().Contain(PassportCompletenessEvaluator.Requirements.Approval);
    }

    [Fact]
    public void Evaluation_is_deterministic()
    {
        var facts = Complete(Component(), Component(part: "ACT-40", lot: "ACT-40-0371", sha: null));

        var a = PassportCompletenessEvaluator.Evaluate(facts);
        var b = PassportCompletenessEvaluator.Evaluate(facts);

        a.Missing.Select(m => m.Code).Should().Equal(b.Missing.Select(m => m.Code));
        a.Requirements.Select(r => (r.Code, r.Satisfied)).Should().Equal(b.Requirements.Select(r => (r.Code, r.Satisfied)));
    }
}
