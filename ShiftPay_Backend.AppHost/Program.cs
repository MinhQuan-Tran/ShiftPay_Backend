var builder = DistributedApplication.CreateBuilder(args);

var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
          ?? "Development";

builder.AddProject<Projects.ShiftPay_Backend>("shiftpay-backend")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", env);

builder.Build().Run();
