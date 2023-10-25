using System.Reactive.Disposables;
using System.Security.Claims;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Security.Claims;

namespace FlowMaker.Services
{
    public class CurrentPrincipalAccessor : ThreadCurrentPrincipalAccessor, ISingletonDependency
    {
        private ClaimsPrincipal? _currentPrincipal;
        protected override ClaimsPrincipal? GetClaimsPrincipal()
        {
            return _currentPrincipal;
        }

        public override IDisposable Change(ClaimsPrincipal principal)
        {
            _currentPrincipal = principal;
            return Disposable.Empty;
        }
    }
}
