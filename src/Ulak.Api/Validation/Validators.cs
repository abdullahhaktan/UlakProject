using FluentValidation;
using Ulak.Core.Domain;
using Ulak.Shared;
using Ulak.Shared.Admin;
using Ulak.Shared.Auth;
using Ulak.Shared.Deliveries;

namespace Ulak.Api.Validation;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(200);
    }
}

public sealed class SignUpRequestValidator : AbstractValidator<SignUpRequest>
{
    public SignUpRequestValidator()
    {
        RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AdminName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20)
            .Must(PhoneNumber.IsValid).WithMessage("Geçerli bir telefon numarası girin.");
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6).MaximumLength(200);
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(6).MaximumLength(200);
    }
}

public sealed class CreateDriverRequestValidator : AbstractValidator<CreateDriverRequest>
{
    public CreateDriverRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20)
            .Must(PhoneNumber.IsValid).WithMessage("Geçerli bir telefon numarası girin.");
    }
}

public sealed class UpdateDriverRequestValidator : AbstractValidator<UpdateDriverRequest>
{
    public UpdateDriverRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20)
            .Must(PhoneNumber.IsValid).WithMessage("Geçerli bir telefon numarası girin.");
    }
}

public sealed class UpdateCompanySettingsRequestValidator : AbstractValidator<UpdateCompanySettingsRequest>
{
    public UpdateCompanySettingsRequestValidator() =>
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
}

public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public sealed class CreateDeliveryRequestValidator : AbstractValidator<CreateDeliveryRequest>
{
    public CreateDeliveryRequestValidator()
    {
        RuleFor(x => x.OrderRef).NotEmpty().MaximumLength(40);
        RuleFor(x => x.RecipientName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.RecipientPhone).MaximumLength(20);
        RuleFor(x => x.AddressText).NotEmpty().MaximumLength(400);
        RuleFor(x => x.Note).MaximumLength(500);
        RuleFor(x => x.Lat).InclusiveBetween(-90, 90).When(x => x.Lat.HasValue);
        RuleFor(x => x.Lng).InclusiveBetween(-180, 180).When(x => x.Lng.HasValue);
        RuleFor(x => x.AssignedDriverId).GreaterThan(0).When(x => x.AssignedDriverId.HasValue);
    }
}

public sealed class AssignDeliveryRequestValidator : AbstractValidator<AssignDeliveryRequest>
{
    public AssignDeliveryRequestValidator() => RuleFor(x => x.DriverId).GreaterThan(0);
}
