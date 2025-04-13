using Microsoft.Identity.Client;

namespace ShiftPay_Backend.Tests
{
    public class AuthenticationService
    {
        private readonly string clientId = "37cdd807-8536-47d3-a393-c455fd68e5f1";  // Your Client ID from Azure AD B2C
        private readonly string authority = "https://shiftpay.b2clogin.com/tfp/shiftpay.onmicrosoft.com/B2C_1_signup_signin";  // Your B2C authority URL
        private readonly string[] scopes = new[] { "https://shiftpay.onmicrosoft.com/api/access_as_user", "openid", "offline_access" };  // Scopes for your app

        public async Task<string> GetAccessToken()
        {
            // Set up the MSAL client application for interactive login (Authorization Code Flow)
            var clientApp = PublicClientApplicationBuilder
                .Create(clientId)
                .WithB2CAuthority(authority)
                .WithRedirectUri("http://localhost:7222")  // Redirect URI (ensure it matches what's configured in Azure AD B2C)
                .Build();

            // Acquire the token interactively (this will prompt the user to log in)
            var result = await clientApp.AcquireTokenInteractive(scopes).ExecuteAsync();

            // Return the access token
            return result.AccessToken;
        }
    }

}
