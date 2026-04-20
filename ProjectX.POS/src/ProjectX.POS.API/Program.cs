using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ProjectX.POS.API;
using ProjectX.POS.Application;
using ProjectX.POS.Infrastructure;
using ProjectX.POS.Infrastructure.Auth;
using ProjectX.POS.Infrastructure.Persistence;
using Scalar.AspNetCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
var iamAuthOptions = builder.Configuration.GetSection(IamAuthenticationOptions.SectionName).Get<IamAuthenticationOptions>()
    ?? throw new InvalidOperationException("IAM authentication configuration is missing.");

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();

builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("ui", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5175"];
        policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddControllers(options =>
    {
        options.Filters.Add<ApplicationExceptionFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.Configure<IamAuthenticationOptions>(builder.Configuration.GetSection(IamAuthenticationOptions.SectionName));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = iamAuthOptions.Issuer,
            ValidateAudience = true,
            ValidAudiences = iamAuthOptions.ValidAudiences,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(iamAuthOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "unique_name",
            RoleClaimType = "role"
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

await app.InitializePersistenceAsync();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/scalar", options =>
    {
        options.WithTitle("ProjectX.POS API");
    });
}

app.UseHttpsRedirection();
app.UseCors("ui");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }))
    .WithName("HealthCheck");
app.MapControllers();

app.Run();
