using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Telnyx;

namespace asp.net_sms_autoresponder
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            string TELNYX_API_KEY = System.Environment.GetEnvironmentVariable("TELNYX_API_KEY");
            TelnyxConfiguration.SetApiKey(TELNYX_API_KEY);

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapPost("/webhooks", async context =>
                {
                    using TextReader reader = new StreamReader(context.Request.Body);
                    string json = await reader.ReadToEndAsync();
                    JsonElement body = JsonSerializer.Deserialize<JsonElement>(json);

                    body.TryGetProperty("data", out JsonElement data);
                    data.TryGetProperty("event_type", out JsonElement eventType);
                    data.TryGetProperty("payload", out JsonElement payload);
                    payload.TryGetProperty("direction", out JsonElement direction);
                    payload.TryGetProperty("text", out JsonElement message);
                    payload.TryGetProperty("from", out JsonElement from);
                    from.TryGetProperty("phone_number", out JsonElement replyToTN);

                    if (eventType.ToString() == "message.received" && direction.ToString() == "inbound") {
                        Console.WriteLine($"Received message: {message}");

                        var preparedReplies = new Dictionary<string, string>
                        {
                            { "ice cream", "I prefer gelato" },
                            { "pizza", "Chicago pizza is the best" }
                        };
                        var defaultReply = "Please send either the word 'pizza' or 'ice cream' for a different response";

                        bool preparedReplyFound = preparedReplies.TryGetValue(message.ToString().ToLower().Trim(), out string preparedReply);
                        if (!preparedReplyFound) {
                            preparedReply = defaultReply;
                        }

                        MessagingSenderIdService service = new MessagingSenderIdService();
                        NewMessagingSenderId options = new NewMessagingSenderId
                        {
                            From = System.Environment.GetEnvironmentVariable("TELNYX_SMS_NUMBER"),
                            To = replyToTN.ToString(),
                            Text = message.ToString()
                        };

                        try
                        {
                            Console.WriteLine($"Will reply with message: {preparedReply}");
                            MessagingSenderId messageResponse = await service.CreateAsync(options);
                            Console.WriteLine(messageResponse);
                        }
                        catch (TelnyxException ex)
                        {
                            Console.WriteLine(ex);
                        }   
                    }
                });
            });
        }
    }
}
