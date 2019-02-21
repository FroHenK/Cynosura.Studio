using FluentValidation;

namespace Cynosura.Studio.Core.Requests.Enums
{
    public class CreateEnumValidator : AbstractValidator<CreateEnum>
    {
        public CreateEnumValidator()
        {
            RuleFor(x => x.Name).MaximumLength(100).NotEmpty().WithName("Name");
            RuleFor(x => x.DisplayName).MaximumLength(100).NotEmpty().WithName("Display Name");
        }

    }
}
