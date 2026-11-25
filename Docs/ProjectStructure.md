MyApp.Web/
├── Controllers/                  # Web API Endpoints (JSON responses)
│   └── AuthApiController.cs      # POST /api/auth/request-otp, /api/auth/verify-otp
├── Data/                         # EF Core & MSSQL Persistence
│   ├── AppDbContext.cs           # DbSets for Users and OtpRequests
│   ├── Entities/
│   │   ├── AppUser.cs            # Id, Email, CreatedAtUtc, LastLoginUtc
│   │   └── OtpCode.cs            # Id, Email, CodeHash, ExpiresAtUtc, IsUsed, Attempts
│   └── Migrations/               # EF Core migrations applied to MonsterASP MSSQL
├── Models/                       # Shared DTOs & ViewModels
│   ├── Auth/
│   │   ├── SendOtpRequest.cs
│   │   └── VerifyOtpRequest.cs
├── Pages/                        # Razor Pages UI
│   ├── Account/
│   │   ├── Login.cshtml          # Step 1: Enter email form
│   │   ├── Login.cshtml.cs
│   │   ├── VerifyOtp.cshtml      # Step 2: Enter 6-digit code form
│   │   └── VerifyOtp.cshtml.cs
│   ├── Shared/
│   │   └── _Layout.cshtml
│   └── Index.cshtml              # Protected dashboard page
├── Services/                     # Core Business Logic (Used by both API & Razor)
│   ├── Interfaces/
│   │   ├── IEmailService.cs
│   │   └── IOtpService.cs
│   ├── ResendEmailService.cs     # Calls Resend API via HttpClient
│   └── OtpService.cs             # Generates, hashes, saves, and validates OTPs
├── wwwroot/                      # Static CSS, JS, images
├── appsettings.json              # Production settings (MonsterASP local DB string + Resend Key)
├── appsettings.Development.json  # Local dev settings (MonsterASP public DB string)
└── Program.cs   