using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Cynosura.Web.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Cynosura.Studio.Web.Infrastructure.Authorization
{
    public class UserModule : IPolicyModule
    {
        public void RegisterPolicies(AuthorizationOptions options)
        {
            options.AddPolicy("ReadUser",
                policy => policy.RequireClaim(ClaimTypes.Role, "Administrator"));
            options.AddPolicy("WriteUser",
                policy => policy.RequireClaim(ClaimTypes.Role, "Administrator"));
        }
    }
}
