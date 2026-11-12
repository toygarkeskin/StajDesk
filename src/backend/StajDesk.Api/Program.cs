using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using StackExchange.Redis;
using StajDesk.Api.Infrastructure;
using StajDesk.Application;
using StajDesk.Application.Abstractions;
using StajDesk.Application.Common;
using StajDesk.Application.Features.Auth;
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
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("OAuth"));

// ---------- Kimlik doğrulama: RS256 JWT (OAuth 2.0 erişim token'ı) ----------
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton<SigningKeyProvider>();
builder.Services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();

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
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256], // "alg: none" ve algoritma karıştırma saldırılarına karşı
            ValidTypes = ["at+jwt"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
            RoleClaimType = "role",
        };
        // 401/403 yanıtları da standart zarfla dönsün.
        o.Events = new JwtBearerEvents
        {
            // WebSocket bağlantısında tarayıcı başlık gönderemez; SignalR token'ı yalnızca /hubs için sorgu parametresiyle taşır.
            // (nginx bu yolda erişim logunu kapatır, Serilog istek logu sorgu dizesini yazmaz → token loglanmaz.)
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs")) ctx.Token = token;
                return Task.CompletedTask;
            },
            OnChallenge = async ctx =>
            {
                ctx.HandleResponse();
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await ctx.Response.WriteAsJsonAsync(ApiResponse.Fail("Oturum bulunamadı veya süresi doldu. Devam etmek için giriş yapın."));
            },
            OnForbidden = async ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                await ctx.Response.WriteAsJsonAsync(ApiResponse.Fail("Bu işlem rolünüze açık değil. Erişim gerekiyorsa sistem yöneticinizden isteyin."));
            },
        };
    });
// İmza anahtarı DI'dan gelir (dosyada kalıcı RSA anahtarı).
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<SigningKeyProvider>((o, keys) => o.TokenValidationParameters.IssuerSigningKey = keys.PublicKey);
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
            ApiResponse.Fail("Kısa sürede çok fazla istek geldi. Bir dakika bekleyip tekrar deneyin."), ct);
});

// nginx arkasında gerçek istemci IP'si (hız sınırı ve denetim kaydı için)
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

// ---------- CORS (geliştirme sırasında Vite dev sunucusu için) ----------
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

builder.Services.AddOpenApi();

// ---------- Gerçek zamanlı bildirimler (SignalR + Redis kanalı) ----------
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, SubUserIdProvider>();
builder.Services.AddHostedService<RealtimeBridge>();

var app = builder.Build();

// ---------- Açılışta migration + seed ----------
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DbSeeder>().MigrateAndSeedAsync();
}

app.UseForwardedHeaders();
// İstek logu hata yakalayıcının DIŞINDA: işlenmiş hatalar (400/401/404) Seq'te gerçek durum koduyla görünür, 500 diye değil.
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
// Güvenlik başlıkları (XSS, clickjacking, MIME sniffing)
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h.XContentTypeOptions = "nosniff";
    h.XFrameOptions = "DENY";
    h["Referrer-Policy"] = "no-referrer";
    if (!ctx.Request.Path.StartsWithSegments("/docs") && !ctx.Request.Path.StartsWithSegments("/openapi"))
        h.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
    await next();
});
app.UseCors();
app.UseAuthentication();
app.UseMiddleware<PermissionLoadingMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();

app.MapOpenApi();
app.MapScalarApiReference("/docs", o => o.WithTitle("StajDesk API"));

app.MapGet("/api/health", async (AppDbContext db, IConnectionMultiplexer redis) =>
{
    var dbOk = await db.Database.CanConnectAsync();
    var body = new { status = dbOk ? "OK" : "DEGRADED", serverTime = DateTime.UtcNow, database = dbOk, redis = redis.IsConnected };
    return dbOk ? Results.Ok(body) : Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable);
}).AllowAnonymous();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();

public partial class Program;
