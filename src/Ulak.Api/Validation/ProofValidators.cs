using FluentValidation;
using Ulak.Core.Domain;
using Ulak.Shared.Proofs;
using Ulak.Shared.Uploads;

namespace Ulak.Api.Validation;

public sealed class PresignRequestValidator : AbstractValidator<PresignRequest>
{
    public PresignRequestValidator()
    {
        RuleFor(x => x.ContentType).NotEmpty();
        RuleFor(x => x.Kind).NotEmpty().Must(k => k is "photo" or "signature")
            .WithMessage("kind must be 'photo' or 'signature'.");
    }
}

public sealed class CreateProofRequestValidator : AbstractValidator<CreateProofRequest>
{
    public CreateProofRequestValidator()
    {
        RuleFor(x => x.ClientUuid).NotEmpty();
        RuleFor(x => x.DeliveryId).GreaterThan(0);
        RuleFor(x => x.Status).Must(ProofStatuses.IsValid)
            .WithMessage("status must be 'Delivered' or 'Failed'.");
        RuleFor(x => x.FailureReason).NotEmpty().MaximumLength(300)
            .When(x => x.Status == ProofStatuses.Failed)
            .WithMessage("failureReason is required when status is 'Failed'.");
        RuleFor(x => x.RecipientSignedName).MaximumLength(150);
        RuleFor(x => x.PhotoUrls).NotNull();
        RuleFor(x => x.PhotoUrls.Count).LessThanOrEqualTo(5)
            .WithMessage("At most 5 photos are allowed.");
        RuleForEach(x => x.PhotoUrls).NotEmpty().MaximumLength(400);
        RuleFor(x => x.SignatureUrl).MaximumLength(400);
        RuleFor(x => x.CapturedLat).InclusiveBetween(-90, 90).When(x => x.CapturedLat.HasValue);
        RuleFor(x => x.CapturedLng).InclusiveBetween(-180, 180).When(x => x.CapturedLng.HasValue);
        RuleFor(x => x.CapturedAt)
            .Must(v => v != default)
            .LessThanOrEqualTo(_ => DateTimeOffset.UtcNow.AddMinutes(5))
            .WithMessage("capturedAt cannot be in the future.");
    }
}
