using EncodeLabs.Inbox.Application.Telegram;
using EncodeLabs.Inbox.Application.IncomingMessages;
using EncodeLabs.Inbox.Application.Realtime;
using EncodeLabs.Inbox.Application.Conversations;
using EncodeLabs.Inbox.Api.Realtime;
using EncodeLabs.Inbox.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddSignalR();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? ["http://localhost:4200"];
    policy.WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
}));
builder.Services.AddScoped<TelegramWebhookHandler>();
builder.Services.AddScoped<ProcessIncomingMessageHandler>();
builder.Services.AddScoped<GetConversationsHandler>();
builder.Services.AddScoped<GetConversationMessagesHandler>();
builder.Services.AddScoped<MarkConversationAsReadHandler>();
builder.Services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseCors();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapHub<ConversationsHub>("/hubs/conversations");

app.Run();
