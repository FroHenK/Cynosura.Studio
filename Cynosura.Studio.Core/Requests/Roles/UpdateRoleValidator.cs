﻿using FluentValidation;

namespace Cynosura.Studio.Core.Requests.Roles
{
    public class UpdateRoleValidator : AbstractValidator<UpdateRole>
    {
        public UpdateRoleValidator()
        {
            RuleFor(x => x.Name).MaximumLength(256).NotEmpty();
        }

    }
}
