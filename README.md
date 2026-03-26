# ShiftPay Backend

ShiftPay Backend is the ASP.NET API for storing and serving user-specific shift data. It powers authenticated shift management, template storage, and workplace metadata for the ShiftPay client, with persistence handled through Azure Cosmos DB.

## Live API

[https://shiftpaybackend.azurewebsites.net/](https://shiftpaybackend.azurewebsites.net/)

## What this backend covers

- Authenticated REST endpoints for shifts, shift templates, and workplace records
- User-scoped data access using the authenticated identity claim as the partition key
- Azure Cosmos DB persistence through Entity Framework Core Cosmos
- Azure AD B2C integration through Microsoft Identity Web
- Fake-auth support for easier local API testing
- Shared service defaults and a dedicated test project in the solution

## Tech stack

- ASP.NET Core
- .NET 10
- Aspire
- Entity Framework Core Cosmos
- Azure Cosmos DB
- Azure AD B2C / Microsoft Identity Web

## Solution layout

- `ShiftPay_Backend/` — main Web API project
- `ShiftPay_Backend.AppHost/` — Aspire app host project
- `ShiftPay_Backend.ServiceDefaults/` — shared hosting and telemetry defaults
- `ShiftPay_Backend.Tests/` — automated test project

## API surface

The main API project exposes three primary resource areas:

- `/api/Shifts`
- `/api/ShiftTemplates`
- `/api/WorkInfos`

All endpoints are scoped to the current signed-in user.

## Local development

### Prerequisites

- .NET 10 SDK
- Azure Cosmos DB Emulator

### Run locally

1. Start the Azure Cosmos DB Emulator.
2. From the repository root, restore packages:
   ```bash
   dotnet restore
   ```
3. Run the API project:
   ```bash
   dotnet run --project ShiftPay_Backend/ShiftPay_Backend.csproj
   ```
4. Open the local API at:
   ```text
   https://localhost:7222
   ```

The development configuration already includes a Cosmos DB emulator connection string in `ShiftPay_Backend/appsettings.Development.json`.

## Related repositories

- [ShiftPay Frontend](https://github.com/MinhQuan-Tran/ShiftPay_Frontend)
- [ShiftPay Documentation](https://github.com/MinhQuan-Tran/ShiftPay_Document)
