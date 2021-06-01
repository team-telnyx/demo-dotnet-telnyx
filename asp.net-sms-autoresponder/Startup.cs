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

            static string GetPreparedReply(string msg)
            {
                var preparedReplies = new Dictionary<string, string>
                {
                    { "ice cream", "I prefer gelato" },
                    { "pizza", "Chicago pizza is the best" }
                };
                var defaultReply = "Please send either the word 'pizza' or 'ice cream' for a different response";

                bool preparedReplyFound = preparedReplies.TryGetValue(msg.ToLower().Trim(), out string preparedReply);
                if (!preparedReplyFound) {
                    preparedReply = defaultReply;
                }
                return preparedReply;
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapPost("/webhooks", async context =>
                {
                    using TextReader reader = new StreamReader(context.Request.Body);
                    string json = await reader.ReadToEndAsync();
                    JsonElement body = JsonSerializer.Deserialize<JsonElement>(json);

                    var data = body.GetProperty("data");
                    var eventType = data.GetProperty("event_type");
                    var payload = data.GetProperty("payload");
                    var direction = payload.GetProperty("direction");
                    var message = payload.GetProperty("text");
                    var from = payload.GetProperty("from");
                    var replyToTN = from.GetProperty("phone_number");

                    if (eventType.ToString() == "message.received" && direction.ToString() == "inbound") {
                        Console.WriteLine($"Received message: {message}");

                        var preparedReply = GetPreparedReply(message.ToString());

                        MessagingSenderIdService service = new MessagingSenderIdService();
                        NewMessagingSenderId options = new NewMessagingSenderId
                        {
                            From = System.Environment.GetEnvironmentVariable("TELNYX_SMS_NUMBER"),
                            To = replyToTN.ToString(),
                            Text = preparedReply.ToString()
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
