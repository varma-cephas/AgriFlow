# AgriFlow
Dot Net Group Project

## Tech Stack

- **Framework:** ASP.NET Core (.NET 10) — Razor Pages + Web API
- **Database & ORM:** SQL Server 2025 (MonsterASP.NET) + Entity Framework Core
- **Email Delivery for OTP:** Resend API (HTTPS outbound via `Resend` SDK)
- **Authentication:** Cookie Authentication (Razor Pages) + JWT Bearer (API endpoints)


## Getting started
1. Setting up database
	- Create a Database in SSMS called `AgriFlowDB`
	- The project in Visual Studio
	- Make a database connection with the `AgriFlowDB`  in visual studio
	- Open the `init.sql` file in the `Schema folder` and select the AgriFlowDB and run the sql file.


## Installed Packages
- `Microsoft.EntityFrameworkCore.SqlServer` — EF Core database provider for SQL Server
- `Microsoft.EntityFrameworkCore.Tools` — Package Manager Console tools for EF Core migrations
- `BCrypt.Net-Next` — Secure password hashing and verification
- `Microsoft.AspNetCore.Authentication.JwtBearer` — JWT authentication for `/api/*` endpoints
- `Resend` — Official SDK for sending OTP emails via Resend