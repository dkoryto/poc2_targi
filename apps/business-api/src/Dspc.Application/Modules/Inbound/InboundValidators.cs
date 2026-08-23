using Dspc.Domain.Common;
using FluentValidation;

namespace Dspc.Application.Modules.Inbound;

public sealed class PatchLineRequestValidator : AbstractValidator<PatchLineRequest>
{
    public PatchLineRequestValidator()
    {
        RuleFor(x => x.Status).Must(s => s is null || Enum.TryParse<PurchaseOrderLineStatus>(s, true, out _)).WithMessage("Unknown status.");
        RuleFor(x => x.ProgressPercent).InclusiveBetween(0, 100).When(x => x.ProgressPercent.HasValue);
        RuleFor(x => x.Quantity).GreaterThan(0).When(x => x.Quantity.HasValue);
        RuleFor(x => x.LotNumber).MaximumLength(64).Matches("^[A-Za-z0-9\\-_/]+$").When(x => !string.IsNullOrEmpty(x.LotNumber));
        RuleFor(x => x.HeatNumber).MaximumLength(64).When(x => x.HeatNumber is not null);
        RuleFor(x => x.Comment).MaximumLength(1000);
        RuleFor(x => x.ExpiresOn).GreaterThan(x => x.ProducedOn!.Value).When(x => x.ExpiresOn.HasValue && x.ProducedOn.HasValue);
    }
}

public sealed class EtaChangeRequestValidator : AbstractValidator<EtaChangeRequest>
{
    public EtaChangeRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Comment).MaximumLength(1000);
        RuleFor(x => x.Eta).Must(d => d > new DateOnly(2000, 1, 1) && d < new DateOnly(2100, 1, 1)).WithMessage("ETA out of range.");
    }
}

public sealed class CreateShipmentRequestValidator : AbstractValidator<CreateShipmentRequest>
{
    public CreateShipmentRequestValidator()
    {
        RuleFor(x => x.PoCode).NotEmpty();
        RuleFor(x => x.LineIds).NotEmpty();
        RuleFor(x => x.Carrier).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Vehicle).MaximumLength(50);
    }
}

public sealed class AddShipmentEventRequestValidator : AbstractValidator<AddShipmentEventRequest>
{
    public AddShipmentEventRequestValidator()
    {
        RuleFor(x => x.Type).Must(s => Enum.TryParse<ShipmentEventType>(s, true, out _)).WithMessage("Unknown event type.");
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.Progress).InclusiveBetween(0, 1).When(x => x.Progress.HasValue);
    }
}

public sealed class CreateLogisticsEventRequestValidator : AbstractValidator<CreateLogisticsEventRequest>
{
    public CreateLogisticsEventRequestValidator()
    {
        RuleFor(x => x.Type).Must(s => Enum.TryParse<LogisticsEventType>(s, true, out _)).WithMessage("Unknown logistics event type.");
        RuleFor(x => x.Severity).Must(s => Enum.TryParse<EventSeverity>(s, true, out _)).WithMessage("Unknown severity.");
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
    }
}
