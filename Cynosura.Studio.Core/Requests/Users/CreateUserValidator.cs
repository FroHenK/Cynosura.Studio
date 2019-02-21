using FluentValidation;

namespace Cynosura.Studio.Core.Requests.Users
{
    public class CreateUserValidator : AbstractValidator<CreateUser>
    {
        public CreateUserValidator()
        {
            RuleFor(x => x.Email).EmailAddress().NotEmpty().WithName("Email");
            RuleFor(x => x.Password).Length(6, 100).NotEmpty().WithName("Password");
            RuleFor(x => x.ConfirmPassword).Equal(x => x.Password).WithMessage("Passwords do not match");
        }
    }
}
