using backend.Data;
using backend.Services;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. Database (EF Core -> AgriFlowDB)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Data Protection (for NationalIdEncrypted) & Core Security Services
var keysDirectory = new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys"));
builder.Services.AddDataProtection()
    .SetApplicationName("AgriFlow")
    .PersistKeysToFileSystem(keysDirectory);
builder.Services.AddScoped<INationalIdProtector, NationalIdProtector>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IEmailService, ResendEmailService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IMarketplaceService, MarketplaceService>();

// 3. Controllers & OpenAPI/Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 4. CORS (React Vite dev server + production domain)
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactClient", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// 5. Pure JWT Bearer Authentication
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured in User Secrets or Environment Variables.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

// 6. Resend Client (ready for Stage 3)
builder.Services.AddHttpClient<Resend.ResendClient>();
builder.Services.Configure<Resend.ResendClientOptions>(o =>
{
    o.ApiToken = builder.Configuration["Resend:ApiKey"] ?? string.Empty;
});
builder.Services.AddTransient<Resend.IResend, Resend.ResendClient>();

var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("ReactClient");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();