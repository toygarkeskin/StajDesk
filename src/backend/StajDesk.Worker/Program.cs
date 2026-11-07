using Serilog;
using StajDesk.Infrastructure;
using StajDesk.Infrastructure.Messaging;
using StajDesk.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((sp, lc) =>
{
    lc.ReadFrom.Configuration(builder.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "StajDesk.Worker")
        .WriteTo.Console();
    var seqUrl = builder.Configuration["Seq:ServerUrl"];
    if (!string.IsNullOrWhiteSpace(seqUrl))
        lc.WriteTo.Seq(seqUrl, apiKey: builder.Configuration["Seq:ApiKey"]);
});

builder.Services.AddPersistence(builder.Configuration);
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddScoped<Mailer>();
builder.Services.AddScoped<NotificationHandler>();
builder.Services.AddHostedService<NotificationConsumer>();

await builder.Build().RunAsync();
