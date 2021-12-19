using System;
using System.Collections.Generic;

namespace HeuristicSearchMethodsSimulation.Models
{
	public record UserWithDictRoles(IdentityUser User, Dictionary<Guid, Guid> Roles);
}
