# AgriFlow
Dot Net Group Project

## Tech Stack & Installed Packages

### Backend 
- **Framework:** ASP.NET Core Web API (.NET 8 / .NET 9)
- **Database & ORM:** SQL Server + Entity Framework Core 
- **NuGet Packages:**
  - `Microsoft.EntityFrameworkCore.SqlServer` — EF Core SQL Server provider
  - `Microsoft.EntityFrameworkCore.Tools` — Package Manager Console tools for EF Core migrations
  - `BCrypt.Net-Next` — Secure password hashing and verification
  - `Microsoft.AspNetCore.Authentication.JwtBearer` — Stateless JWT authentication for API routes
  - `Resend` — Official SDK for sending transactional OTP emails via Resend

### Frontend 
- **Framework:** React + TypeScript (Vite)
- **Libraries:** `react-router-dom` (client-side routing & role guards), `axios` (API client with JWT interceptor)


## Getting started
1. Setting Up the Database (AgriFlowDB)
    - Create the Database in SSMS:
        - Open SQL Server Management Studio (SSMS) and connect to your local server (localhost or .\SQLEXPRESS).
        - Right-click Databases → New Database..., name it AgriFlowDB, and click OK.
    - Open the Backend Solution in Visual Studio:
    	- Connect to AgriFlowDB in Visual Studio:
        - Open View → SQL Server Object Explorer.
        - Click Add SQL Server, connect to your local SQL Server instance, and expand Databases to confirm AgriFlowDB is listed.
    - Run the Initial Schema Script:
        - Open Schema/init.sql in SSMS (or inside Visual Studio).
        - Ensure AgriFlowDB is selected in the active database dropdown on the top toolbar.
        - Click Execute (F5) to create the initial database tables and seed data.

2. Configuring Secrets via Visual Studio (Manage User Secrets)
To prevent database passwords, JWT signing keys, and Resend API keys from ever being committed to GitHub, store them outside the repository using Visual Studio's Manage User Secrets UI:
- In Visual Studio Solution Explorer, expand the server solution and locate the AgriFlow.Api project.
- Right-click AgriFlow.Api and select Manage User Secrets from the context menu.
- Paste the following JSON into secrets.json and replace the placeholder values with your own:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=AgriFlowDB;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Resend": {
    "ApiKey": "re_your_dev_resend_api_key",
    "FromEmail": "AgriFlow <onboarding@resend.dev>"
  },
  "Jwt": {
    "Key": "replace-with-a-random-local-dev-secret-at-least-32-chars",
    "Issuer": "https://localhost:7179",
    "Audience": "http://localhost:5173"
  }
}
```
Notes
- If you use SQL Server Express, change Server=localhost to Server=.\\SQLEXPRESS (or paste the exact connection string you copied from SQL Server Object Explorer).
- During local development, keep FromEmail set to onboarding@resend.dev unless you have verified a custom domain in your Resend account.
- Save secrets.json. ASP.NET Core automatically loads these values in Development mode without modifying appsettings.json.


3. Sync Migrations & Run the Web API
- In Visual Studio, open Tools → NuGet Package Manager → Package Manager Console.
In Powershell run the command below
`Update-Database`

Start the API from the repository root with the HTTPS profile:

```powershell
dotnet run --project backend/backend.csproj --launch-profile https
```

4. Setting Up & Running the React Frontend
- Open a terminal inside the frontend/ directory:
Run:
```sh
cd frontend
npm install

cp .env.example .env

npm run dev
```

5. Deployment Architecture
- Database & Web API : (MonsterASP.NET)
- React Frontend : (Cloudflare Pages)
- OTP Email Delivery (Resend)
