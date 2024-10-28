using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Net.WebSockets;
using System.Text;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;

var builder = WebApplication.CreateBuilder(args);

// Load environment variables
var OPENAI_API_KEY = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var PORT = Environment.GetEnvironmentVariable("PORT") != null ? int.Parse(Environment.GetEnvironmentVariable("PORT")) : 8000;

if (string.IsNullOrEmpty(OPENAI_API_KEY))
{
    Console.WriteLine("Missing OpenAI API key. Please set it in the environment variables.");
    return;
}

// Constants
const string SYSTEM_MESSAGE = "You are a helpful and bubbly AI assistant who loves to chat about anything the user is interested about and is prepared to offer them facts.";
const string VOICE = "alloy";

// List of Event Types to log to the console
var LOG_EVENT_TYPES = new List<string>
{
    "response.content.done",
    "rate_limits.updated",
    "response.done",
    "input_audio_buffer.committed",
    "input_audio_buffer.speech_stopped",
    "input_audio_buffer.speech_started",
    "session.created"
};

// Add services to the container
builder.Services.AddControllers();

// Add middleware for CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

// Build the app
var app = builder.Build();

// Configure the HTTP request pipeline
app.UseCors();

// Enable WebSockets
app.UseWebSockets();

// Root route
app.MapGet("/", async context =>
{
    await context.Response.WriteAsJsonAsync(new { message = "Telnyx Media Stream Server is running!" });
});

// Route for Telnyx to handle incoming and outgoing calls
app.MapPost("/inbound", async context =>
{
    Console.WriteLine("Incoming call received");
    var headers = context.Request.Headers;
    var host = headers["Host"].ToString();

    // Construct the correct relative path to the texml.xml file
    var texmlPath = Path.Combine(AppContext.BaseDirectory, "texml.xml");

    if (!File.Exists(texmlPath))
    {
        Console.WriteLine($"File not found at: {texmlPath}");
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync("TeXML file not found");
        return;
    }

    var texmlResponse = await File.ReadAllTextAsync(texmlPath);
    texmlResponse = texmlResponse.Replace("{host}", host);
    Console.WriteLine($"TeXML Response: {texmlResponse}");

    context.Response.ContentType = "text/xml";
    await context.Response.WriteAsync(texmlResponse);
});

// WebSocket route for media-stream
app.Map("/media-stream", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        using var telnyxWebSocket = await context.WebSockets.AcceptWebSocketAsync();
        Console.WriteLine("Client connected");

        using var openAiWebSocket = new ClientWebSocket();
        openAiWebSocket.Options.SetRequestHeader("Authorization", $"Bearer {OPENAI_API_KEY}");
        openAiWebSocket.Options.SetRequestHeader("OpenAI-Beta", "realtime=v1");

        try
        {
            await openAiWebSocket.ConnectAsync(new Uri("wss://api.openai.com/v1/realtime?model=gpt-4o-realtime-preview-2024-10-01"), CancellationToken.None);

            async Task SendSessionUpdate()
            {
                var sessionUpdate = new
                {
                    type = "session.update",
                    session = new
                    {
                        turn_detection = new { type = "server_vad" },
                        input_audio_format = "g711_ulaw",
                        output_audio_format = "g711_ulaw",
                        voice = VOICE,
                        instructions = SYSTEM_MESSAGE,
                        modalities = new[] { "text", "audio" },
                        temperature = 0.8
                    }
                };
                var message = JsonSerializer.Serialize(sessionUpdate);
                Console.WriteLine("Sending session update: " + message);
                var bytes = Encoding.UTF8.GetBytes(message);
                await openAiWebSocket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            }

            // Wait to send session update after WebSocket connection is stable
            await Task.Delay(250);
            await SendSessionUpdate();

            async Task ReceiveOpenAiMessages()
            {
                var buffer = new byte[8192];
                while (openAiWebSocket.State == WebSocketState.Open)
                {
                    var result = await openAiWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await openAiWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    }
                    else
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        try
                        {
                            var response = JsonSerializer.Deserialize<JsonElement>(message);
                            var type = response.GetProperty("type").GetString();
                            if (LOG_EVENT_TYPES.Contains(type))
                            {
                                Console.WriteLine($"Received event: {type}", response);
                            }
                            if (type == "session.updated")
                            {
                                Console.WriteLine("Session updated successfully:", response);
                            }
                            if (type == "response.audio.delta" && response.TryGetProperty("delta", out var delta))
                            {
                                var audioDelta = new
                                {
                                    @event = "media",
                                    media = new
                                    {
                                        payload = delta.GetString()
                                    }
                                };
                                var audioMessage = JsonSerializer.Serialize(audioDelta);
                                var audioBytes = Encoding.UTF8.GetBytes(audioMessage);
                                await telnyxWebSocket.SendAsync(audioBytes, WebSocketMessageType.Text, true, CancellationToken.None);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Error processing OpenAI message:", ex, "Raw message:", message);
                        }
                    }
                }
            }

            async Task ReceiveTelnyxMessages()
            {
                var buffer = new byte[8192];
                while (telnyxWebSocket.State == WebSocketState.Open)
                {
                    var result = await telnyxWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await telnyxWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    }
                    else
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        var data = JsonSerializer.Deserialize<JsonElement>(message);
                        var eventType = data.GetProperty("event").GetString();

                        if (eventType == "media")
                        {
                            if (openAiWebSocket.State == WebSocketState.Open)
                            {
                                if (data.TryGetProperty("media", out var media) && media.TryGetProperty("payload", out var payload))
                                {
                                    var base64Audio = payload.GetString();
                                    var audioAppend = new
                                    {
                                        type = "input_audio_buffer.append",
                                        audio = base64Audio
                                    };
                                    var audioMessage = JsonSerializer.Serialize(audioAppend);
                                    var audioBytes = Encoding.UTF8.GetBytes(audioMessage);
                                    await openAiWebSocket.SendAsync(audioBytes, WebSocketMessageType.Text, true, CancellationToken.None);
                                }
                            }
                        }
                        else if (eventType == "start")
                        {
                            var streamId = data.GetProperty("stream_id").GetString();
                            Console.WriteLine($"Incoming stream has started: {streamId}");
                        }
                        else
                        {
                            Console.WriteLine($"Received non-media event: {eventType}");
                        }
                    }
                }
            }

            // Run OpenAI and Telnyx message receivers concurrently
            await Task.WhenAll(ReceiveOpenAiMessages(), ReceiveTelnyxMessages());
        }
        catch (WebSocketException ex)
        {
            Console.WriteLine("WebSocket error:", ex);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception:", ex);
        }
        finally
        {
            if (telnyxWebSocket.State != WebSocketState.Closed)
            {
                await telnyxWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
            if (openAiWebSocket.State != WebSocketState.Closed)
            {
                await openAiWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
            Console.WriteLine("Client disconnected.");
        }
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

// Start the server
app.Run($"http://0.0.0.0:{PORT}");