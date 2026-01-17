using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace ShiftPay_Backend.Auth
{
    public class FakeAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        [Obsolete]
        public FakeAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
            : base(options, logger, encoder, clock) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // Allow tests to override the userId via a custom header
            var userIdFromHeader = Context.Request.Headers["X-Test-UserId"].FirstOrDefault();
            var userId = string.IsNullOrEmpty(userIdFromHeader) ? "test-user-id" : userIdFromHeader;

            var claims = new[]
            {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, "Test User")
        };

            var identity = new ClaimsIdentity(claims, "FakeAuth");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "FakeAuth");

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
