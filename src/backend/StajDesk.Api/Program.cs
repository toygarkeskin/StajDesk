using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using StackExchange.Redis;
using StajDesk.Api.Infrastructure;
using StajDesk.Application;
using StajDesk.Application.Abstractions;
using StajDesk.Application.Common;
using StajDesk.Infrastructure;
using StajDesk.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ---------- Loglama: Serilog → Console + Seq ----------
builder.Services.AddSerilog((sp, lc) =>
{
    lc.ReadFrom.Configuration(builder.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "StajDesk.Api")
        .WriteTo.Console();
    var seqUrl = builder.Configuration["Seq:ServerUrl"];
    if (!string.IsNullOrWhiteSpace(seqUrl))
        lc.WriteTo.Seq(seqUrl, apiKey: builder.Configuration["Seq:ApiKey"]);
});

// ---------- Katmanlar ----------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();

// ---------- Kimlik doğrulama (JWT) ----------
var jwtSection = builder.Configuration.GetSection("Jwt");
builder.Services.Configure<JwtOptions>(jwtSection);
var jwt = jwtSection.Get<JwtOptions>() ?? new JwtOptions();
if (jwt.Key.Length < 32)
    throw new InvalidOperationException("Jwt:Key en az 32 karakter olmalıdır. .env dosyasında veya user-secrets ile tanımlayınız.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "name",
            RoleClaimType = JwtTokenService.RoleClaim
        };
        // 401/403 yanıtları da standart zarfla dönsün.
        o.Events = new JwtBearerEvents
        {
            OnChallenge = async ctx =>
            {
                ctx.HandleResponse();
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await ctx.Response.WriteAsJsonAsync(ApiResponse.Fail("Oturum bulunamadı veya süresi doldu. Devam etmek için giriş yapın."));
            },
            OnForbidden = async ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                await ctx.Response.WriteAsJsonAsync(ApiResponse.Fail("Bu işlem rolünüze açık değil. Erişim gerekiyorsa yöneticinizden isteyin."));
            }
        };
    });
builder.Services.AddAuthorization();

// ---------- MVC, doğrulama, hata yönetimi ----------
builder.Services.AddControllers(o => o.Filters.Add<ValidationFilter>())
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = ApiBehavior.InvalidModelState);
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// IP başına genel koruma (e-posta bazlı brute-force koruması ayrıca Redis'te yapılır).
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) }));
    o.OnRejected = async (ctx, ct) =>
        await ctx.HttpContext.Response.WriteAsJsonAsync(
            ApiResponse.Fail("Bu hesap için çok sayıda başarısız giriş denemesi oldu. Güvenliğiniz için girişi birkaç dakika sonra tekrar deneyin."), ct);
});

// ---------- CORS (geliştirme sırasında Vite dev sunucusu için) ----------
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddOpenApi();

var app = builder.Build();

// ---------- Açılışta migration + seed ----------
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DbSeeder>().MigrateAndSeedAsync();
}

app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapOpenApi();
app.MapScalarApiReference("/docs", o => o.WithTitle("StajDesk API"));

app.MapGet("/api/health", async (AppDbContext db, IConnectionMultiplexer redis) =>
{
    var dbOk = await db.Database.CanConnectAsync();
    var redisOk = redis.IsConnected;
    var body = new { status = dbOk ? "OK" : "DEGRADED", serverTime = DateTime.UtcNow, database = dbOk, redis = redisOk };
    return dbOk ? Results.Ok(body) : Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable);
}).AllowAnonymous();

app.MapControllers();

app.Run();

public partial class Program;
