using System;
using System.IO;
using Telnyx;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using Telnyx.net.Services.Calls.CallCommands;

namespace asp_net_IVR {
    public class WebhookHelpers
    {
        public static async Task<CallControlWebhook> deserializeWebhook(HttpRequest request)
        {
            string json;
            using (var reader = new StreamReader(request.Body))
            {
                json = await reader.ReadToEndAsync();
            }
            CallControlWebhook myDeserializedClass = JsonSerializer.Deserialize<CallControlWebhook>(json);
            return myDeserializedClass;
        }
        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        public static string Base64Decode(string base64EncodedData)
        {
            if (base64EncodedData == null) {
                return "";
            }
            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }
    }
    [ApiController]
    [Route("call-control/[controller]")]
    public class InboundController : ControllerBase
    {

        [HttpPost]
        [Consumes("application/json")]
        public async Task<string> CallControlInboundWebhook()
        {
            CallControlWebhook webhook = await WebhookHelpers.deserializeWebhook(this.Request);
            String callControlId = webhook.data.payload.call_control_id;
            CallControlService callControlService = new CallControlService();
            callControlService.CallControlId = callControlId;
            String webhookClientState = WebhookHelpers.Base64Decode(webhook.data.payload.client_state);
            if (webhookClientState == "outbound") {
                Console.WriteLine($"Received outbound event: {webhook.data.event_type}");
                return "";
            }
            switch (webhook.data.event_type){
                case "call.initiated":
                    CallControlAnswerService answerService = new CallControlAnswerService();
                    CallControlAnswerOptions answerOptions = new CallControlAnswerOptions();
                    await answerService.CreateAsync(callControlId, answerOptions);
                    break;
                case "call.answered":
                    CallControlGatherUsingSpeakService gatherUsingSpeakService = new CallControlGatherUsingSpeakService();
                    CallControlGatherUsingSpeakOptions gatherUsingSpeakOptions = new CallControlGatherUsingSpeakOptions(){
                        Language = "en-US",
                        Voice = "female",
                        Payload = "Please enter the 10 digit phone number you would like to dial",
                        InvalidPayload = "Sorry, I didn't get that",
                        MaximumDigits = 11,
                        MinimumDigits = 10,
                        ValidDigits = "0123456789"
                    };
                    await gatherUsingSpeakService.CreateAsync(callControlId, gatherUsingSpeakOptions);
                    break;
                case "call.gather.ended":
                    String digits = webhook.data.payload.digits;
                    String phoneNumber = $"+1{digits}";
                    String outboundClientState = WebhookHelpers.Base64Encode("outbound");
                    CallControlTransferService transferService = new CallControlTransferService();
                    CallControlTransferOptions transferOptions = new CallControlTransferOptions(){
                        To = phoneNumber,
                        ClientState = outboundClientState
                    };
                    await transferService.CreateAsync(callControlId, transferOptions);
                    break;
                default:
                    Console.WriteLine($"Non-handled Event: {webhook.data.event_type}");
                    break;
            }
            return "";
        }
    }
}