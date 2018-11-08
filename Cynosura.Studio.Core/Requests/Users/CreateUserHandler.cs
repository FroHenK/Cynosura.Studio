using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Cynosura.Studio.Core.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Cynosura.Studio.Core.Requests.Users
{
    public class CreateUserHandler : IRequestHandler<CreateUser, int>
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<Role> _roleManager;
        private readonly IMapper _mapper;

        public CreateUserHandler(UserManager<User> userManager, RoleManager<Role> roleManager, IMapper mapper)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _mapper = mapper;
        }

        public async Task<int> Handle(CreateUser request, CancellationToken cancellationToken)
        {
            var user = _mapper.Map<CreateUser, User>(request);
            user.UserName = request.Email;
            var result = await _userManager.CreateAsync(user, request.Password);
            result.CheckIfSucceeded();

            if (request.RoleIds.Any())
            {
                foreach (var roleId in request.RoleIds)
                {
                    var role = await _roleManager.FindByIdAsync(roleId.ToString());
                    result = await _userManager.AddToRoleAsync(user, role.ToString());
                    result.CheckIfSucceeded();
                }
            }
            return user.Id;
        }

    }
}
