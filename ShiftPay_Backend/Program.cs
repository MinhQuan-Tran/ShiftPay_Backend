using Azure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using ShiftPay_Backend.Data;

var builder = WebApplication.CreateBuilder(args);

// Key Vault
var keyVaultUriString = Environment.GetEnvironmentVariable("KeyVaultUri");
if (!string.IsNullOrEmpty(keyVaultUriString))
{
    var keyVaultEndpoint = new Uri(keyVaultUriString);
    builder.Configuration.AddAzureKeyVault(keyVaultEndpoint, new DefaultAzureCredential());
}

// Cosmos DB
builder.Services.AddDbContext<ShiftPay_BackendContext>(options =>
    options.UseCosmos(
        builder.Configuration["CosmosDB-ConnectionString-Primary"] ??
        builder.Configuration["CosmosDB-ConnectionString-Secondary"] ??
        throw new InvalidOperationException("Connection string not found."),
        builder.Configuration["DatabaseName"] ??
        throw new InvalidOperationException("Database name not found.")
    )
);

builder.AddServiceDefaults();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(
        options =>
        {
            builder.Configuration.Bind("AzureAd", options);
            options.TokenValidationParameters.NameClaimType = "name";
        },
        options =>
        {
            builder.Configuration.Bind("AzureAd", options);
            options.TokenValidationParameters.NameClaimType = "name";
        }
    );

builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Debug);
});

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // Runtime OpenAPI for dev
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
