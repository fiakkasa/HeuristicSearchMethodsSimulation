using HeuristicSearchMethodsSimulation.Interfaces;
using HeuristicSearchMethodsSimulation.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using IdentityRole = HeuristicSearchMethodsSimulation.Models.IdentityRole;
using IdentityUser = HeuristicSearchMethodsSimulation.Models.IdentityUser;

namespace HeuristicSearchMethodsSimulation.Services
{
    public class AccessControlService : IDisposable, IAccessControlService
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<AccessControlService> _logger;
        private readonly BehaviorSubject<string?> _searchTokenSubject = new(default);
        private string? _searchToken;
        private IDisposable? _searchTokenDisposable;
        private bool _isInit;
        private bool _isDisposed;
        private event Action? OnStateChangeDelegate;

        public event Action? OnStateChange
        {
            add
            {
                OnStateChangeDelegate += value;
            }
            remove
            {
                OnStateChangeDelegate -= value;
            }
        }

        public bool Loading { get; private set; }
        public bool Searching { get; private set; }
        public Dictionary<Guid, Guid> OperationInProgress { get; } = new();
        public List<UserWithDictRoles> Users { get; } = new();
        public List<IdentityRole> Roles { get; } = new();
        public long Count { get; private set; }
        public string? SearchToken => _searchToken;
        public AccessControlService(
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<AccessControlService> logger
        )
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
        }

        public async Task Init()
        {
            try
            {
                if (_isInit) return;
                _isInit = true;

                if (_searchTokenDisposable is { }) _searchTokenDisposable.Dispose();

                _searchTokenDisposable =
                    _searchTokenSubject
                        .Skip(1)
                        .DistinctUntilChanged()
                        .Throttle(TimeSpan.FromMilliseconds(600))
                        .Subscribe(
                            async searchToken => await SetSearch(searchToken).ConfigureAwait(true),
                            error => _logger.LogError(error, error.Message)
                        );

                Loading = true;
                OnStateChangeDelegate?.Invoke();

                await Task.Delay(500).ConfigureAwait(true);
                Roles.Clear();
                Roles.AddRange(
                    await _roleManager.Roles
                        .ToAsyncEnumerable()
                        .ToListAsync(_cts.Token)
                        .ConfigureAwait(true)
                );
                await SetUsers().ConfigureAwait(true);

                Loading = false;
                OnStateChangeDelegate?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                _isInit = false;
            }
        }

        public void Search(string? searchToken = default) => _searchTokenSubject.OnNext(searchToken);

        public Task ClearSearch() => SetSearch();

        private async Task SetSearch(string? searchToken = default)
        {
            Searching = true;
            OnStateChangeDelegate?.Invoke();

            _searchToken = searchToken;
            await SetUsers().ConfigureAwait(true);

            Searching = false;
            OnStateChangeDelegate?.Invoke();
        }

        private async Task SetUsers()
        {
            try
            {
                var users =
                    await (
                        _searchToken?.Trim().ToLowerInvariant() is { Length: > 0 } token
                            ? _userManager.Users
                                .Where(x => x.UserName.ToLowerInvariant().Contains(token))
                                .OrderByDescending(x => x.CreatedOn)
                            : _userManager.Users
                                .OrderByDescending(x => x.CreatedOn)
                    )
                    .ToAsyncEnumerable()
                    .Select(user => new UserWithDictRoles(user, user.Roles.Distinct().ToDictionary(x => x, x => x)))
                    .ToListAsync(_cts.Token)
                    .ConfigureAwait(true);

                Users.Clear();
                Users.AddRange(users);
                Count = users.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        public async Task AddToRole(IdentityUser user, string role)
        {
            try
            {
                if (OperationInProgress.ContainsKey(user.Id)) return;

                OperationInProgress[user.Id] = user.Id;
                OnStateChangeDelegate?.Invoke();

                await _userManager.RemoveFromRoleAsync(user, role).ConfigureAwait(true);
                await SetUsers().ConfigureAwait(true);

                OperationInProgress.Remove(user.Id);
                OnStateChangeDelegate?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        public async Task RemoveFromRole(IdentityUser user, string role)
        {
            try
            {
                if (OperationInProgress.ContainsKey(user.Id)) return;

                OperationInProgress[user.Id] = user.Id;
                OnStateChangeDelegate?.Invoke();

                await _userManager.AddToRoleAsync(user, role).ConfigureAwait(true);
                await SetUsers().ConfigureAwait(true);

                OperationInProgress.Remove(user.Id);
                OnStateChangeDelegate?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;

            _cts.Cancel();
            _searchTokenDisposable?.Dispose();
            _isDisposed = true;
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
