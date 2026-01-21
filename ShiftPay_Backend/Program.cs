using Azure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using ShiftPay_Backend.Auth;
using ShiftPay_Backend.Data;

var builder = WebApplication.CreateBuilder(args);

// Support environment-based config
builder.Configuration
	.AddJsonFile("appsettings.json", optional: true)
	.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true);

if (builder.Environment.IsProduction())
{
	// Use KeyVault for DB Connection String
	var keyVaultUriString = Environment.GetEnvironmentVariable("KeyVaultUri");
	if (!string.IsNullOrEmpty(keyVaultUriString))
	{
		var keyVaultEndpoint = new Uri(keyVaultUriString);
		builder.Configuration.AddAzureKeyVault(keyVaultEndpoint, new DefaultAzureCredential());
	}
}

// Cosmos DB
builder.Services.AddDbContext<ShiftPay_BackendContext>(options =>
{
	var endpoint = builder.Configuration["Cosmos:Endpoint"];
	var key = builder.Configuration["Cosmos:Key"];
	var databaseName = builder.Configuration["Cosmos:DatabaseName"] ?? builder.Configuration["DatabaseName"];

	if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(databaseName))
	{
		options.UseCosmos(endpoint, key, databaseName);
		return;
	}

	options.UseCosmos(
		builder.Configuration["CosmosDB-ConnectionString-Primary"] ??
		builder.Configuration["CosmosDB-ConnectionString-Secondary"] ??
		throw new InvalidOperationException("Connection string not found."),
		databaseName ?? throw new InvalidOperationException("Database name not found."));
});

builder.AddServiceDefaults();

var useFakeAuth = builder.Configuration.GetValue<bool>("Authentication:UseFake");

if (useFakeAuth)
{
	builder.Services.AddAuthentication(options =>
	{
		options.DefaultAuthenticateScheme = "FakeAuth";
		options.DefaultChallengeScheme = "FakeAuth";
	})
	.AddScheme<AuthenticationSchemeOptions, FakeAuthHandler>("FakeAuth", _ => { })
	.AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
}
else
{
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
}
builder.Services.AddAuthorization();

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

app.MapOpenApi();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
