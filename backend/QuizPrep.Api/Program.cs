using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuizPrep.Api.Common;
using QuizPrep.Api.Data;
using QuizPrep.Api.Domain;
using QuizPrep.Api.Features.Ai;
using QuizPrep.Api.Features.Attempts;
using QuizPrep.Api.Features.Auth;
using QuizPrep.Api.Features.Import;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwt = jwtSection.Get<JwtOptions>() ?? throw new InvalidOperationException("Thiếu cấu hình Jwt.");
if (jwt.SigningKey.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey phải dài ít nhất 32 ký tự.");

builder.Services.Configure<JwtOptions>(jwtSection);
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = jwt.GetSecurityKey(),
            NameClaimType = AppClaims.Name,
            RoleClaimType = AppClaims.Role,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<ImportService>();
builder.Services.AddScoped<QuestionPicker>();
builder.Services.AddScoped<AttemptService>();
builder.Services.AddSingleton<AiExplanationService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AppProblemExceptionHandler>();
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    await DbSeeder.MigrateAndSeedAsync(app.Services);
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
