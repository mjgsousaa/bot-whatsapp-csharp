using EvolutionBot.Api.Configuration;
using EvolutionBot.Api.DTOs.EvolutionApi;
using EvolutionBot.Api.DTOs;
using EvolutionBot.Api.Services;
using Supabase;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Channels;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Configure settings
builder.Services.Configure<SupabaseSettings>(builder.Configuration.GetSection("Supabase"));
builder.Services.Configure<EvolutionApiSettings>(builder.Configuration.GetSection("EvolutionApi"));

// Register HttpClient for EvolutionApiService
builder.Services.AddHttpClient<EvolutionApiService>();

// Register Supabase Client
builder.Services.AddSingleton(provider =>
{
    var supabaseSettings = provider.GetRequiredService<IOptions<SupabaseSettings>>().Value;
    var url = supabaseSettings.Url;
    var key = supabaseSettings.Key;
    var options = new SupabaseOptions
    {
        AutoConnectRealtime = true
    };
    return new Client(url, key, options);
});

// Register custom services
builder.Services.AddScoped<SupabaseService>();
builder.Services.AddScoped<EvolutionApiService>();

// Register message queue and background service
builder.Services.AddSingleton<IMessageQueueService, ChannelMessageQueueService>();
builder.Services.AddHostedService<MessageProcessingBackgroundService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Define API Endpoints
app.MapGet("/", () => "Evolution Bot API is running!");

// Evolution API Endpoints
app.MapPost("/evolution/instance", async (CreateInstanceRequest request, EvolutionApiService evolutionService) =>
{
    var response = await evolutionService.CreateInstance(request.InstanceName);
    return Results.Ok(response);
});

app.MapPost("/evolution/send-message/{instanceId}", async (string instanceId, SendMessageRequest request, EvolutionApiService evolutionService) =>
{
    var success = await evolutionService.SendMessage(instanceId, request);
    return success ? Results.Ok() : Results.BadRequest("Failed to send message.");
});

// Webhook Endpoint
app.MapPost("/webhook/evolution", async (WebhookPayload payload, SupabaseService supabaseService, EvolutionApiService evolutionService, ILogger<Program> logger) =>
{
    logger.LogInformation("Webhook recebido: {PayloadId}", payload.Id);

    // Save message to Supabase
    var message = new EvolutionBot.Api.Models.Message
    {
        Id = Guid.NewGuid(),
        Sender = payload.Data.From,
        Receiver = payload.Data.To,
        Content = payload.Data.Message?.Conversation ?? payload.Data.Message?.ExtendedTextMessage?.Text,
        Timestamp = DateTimeOffset.FromUnixTimeSeconds(payload.Data.MessageTimestamp).DateTime,
        Status = payload.Data.Status ?? "received", // Assuming 'received' for incoming messages, 'status' for status updates
        MessageType = "text", // Assuming text for now
        InstanceId = payload.Instance
    };
    await supabaseService.SaveMessage(message);

    // Process confirmation
    if (message.Content != null && (message.Content.Equals("1", StringComparison.OrdinalIgnoreCase) || message.Content.Equals("Confirmar", StringComparison.OrdinalIgnoreCase)))
    {
        var appointment = await supabaseService.GetAppointmentByPhoneNumber(message.Sender);
        if (appointment != null)
        {
            await supabaseService.UpdateAppointmentStatus(appointment.Id, "confirmado");
            // Send confirmation message back
            var confirmationMessage = new SendMessageRequest
            {
                Number = message.Sender,
                TextMessage = "Seu agendamento foi confirmado com sucesso!"
            };
            await evolutionService.SendMessage(message.InstanceId, confirmationMessage);
        }
    }

    return Results.Ok();
});

// Mass Messaging Endpoint
app.MapPost("/messages/enqueue", (SendMessageRequest request, IMessageQueueService messageQueueService, ILogger<Program> logger) =>
{
    // For simplicity, assuming a default instanceId for mass messages or it comes from the request
    // In a real scenario, you might have a dedicated instance for mass sending or pass it in the request.
    var instanceId = "default_mass_instance"; 
    messageQueueService.EnqueueMessage(new DTOs.QueuedMessage
    {
        InstanceId = instanceId,
        Number = request.Number,
        TextMessage = request.TextMessage,
        QueuedTime = DateTime.UtcNow
    });
    logger.LogInformation("Mensagem enfileirada para {Number}", request.Number);
    return Results.Accepted();
});

app.Run();
