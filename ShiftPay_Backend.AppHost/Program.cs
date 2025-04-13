var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.ShiftPay_Backend>("shiftpay-backend");

builder.Build().Run();
