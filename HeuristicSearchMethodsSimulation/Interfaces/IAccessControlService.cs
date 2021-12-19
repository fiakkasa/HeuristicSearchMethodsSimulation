using HeuristicSearchMethodsSimulation.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HeuristicSearchMethodsSimulation.Interfaces
{
	public interface IAccessControlService
	{
		long Count { get; }
		bool Loading { get; }
		Dictionary<Guid, Guid> OperationInProgress { get; }
		List<UserWithDictRoles> Users { get; }
		string? SearchToken { get; }
        bool Searching { get; }
        List<IdentityRole> Roles { get; }

        event Action? OnStateChange;

		Task AddToRole(IdentityUser user, string role);
        Task ClearSearch();
        Task Init();
		Task RemoveFromRole(IdentityUser user, string role);
		void Search(string? searchToken = null);
	}
}
