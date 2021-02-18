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
    }

    [ApiController]
    [Route("call-control/[controller]")]
    public class OutboundController : ControllerBase
    {
        // POST messaging/Inbound
        [HttpPost]
        [Consumes("application/json")]
        public async Task<string> CallControlOutboundWebhook()
        {
            CallControlWebhook webhook = await WebhookHelpers.deserializeWebhook(this.Request);
            Console.WriteLine($"Received Webhook for call with call_control_id: {webhook.data.payload.call_control_id}");
            return "";
        }
    }

    [ApiController]
    [Route("call-control/[controller]")]
    public class InboundController : ControllerBase
    {

        private string TELNYX_API_KEY = System.Environment.GetEnvironmentVariable("TELNYX_API_KEY");
        [HttpPost]
        [Consumes("application/json")]
        public async Task<string> CallControlInboundWebhook()
        {
            CallControlWebhook webhook = await WebhookHelpers.deserializeWebhook(this.Request);
            CallControlService callControlService = new CallControlService();
            callControlService.CallControlId = webhook.data.payload.call_control_id;
            UriBuilder uriBuilder = new UriBuilder(Request.Scheme, Request.Host.ToString());
            uriBuilder.Path = "call-control/outbound";
            string outboundUrl = uriBuilder.ToString();
            switch (webhook.data.event_type){
                case "call.initiated":
                    CallControlAnswerOptions answerOptions = new CallControlAnswerOptions();
                    callControlService.Answer(answerOptions);
                    break;
                case "call.answered":
                    CallControlGatherUsingSpeakOptions gatherUsingSpeakOptions = new CallControlGatherUsingSpeakOptions(){

                    };
                    break;
                case "call.gather.ended":
                    break;

            }
            // string to = webhook.data.payload.to[0].phone_number;
            // string from = webhook.data.payload.from.phone_number;
            // List<MediaItem> media = webhook.data.payload.media;
            // List<string> files = new List<string>();
            // List<string> mediaUrls = new List<string>();
            // if (media != null)
            // {
            //     foreach (var item in media)
            //     {
            //         string path = await WebhookHelpers.downloadMediaAsync("./", item.hash_sha256, new Uri(item.url));
            //         files.Add(path);
            //         string mediaUrl = await WebhookHelpers.UploadFileAsync(path);
            //         mediaUrls.Add(mediaUrl);
            //     }
            // }
            // TelnyxConfiguration.SetApiKey(TELNYX_API_KEY);
            // MessagingSenderIdService service = new MessagingSenderIdService();
            // NewMessagingSenderId options = new NewMessagingSenderId
            // {
            //     From = to,
            //     To = from,
            //     Text = "Hello, World!",
            //     WebhookUrl = dlrUri,
            //     UseProfileWebhooks = false,
            //     MediaUrls = mediaUrls
            // };
            // MessagingSenderId messageResponse = await service.CreateAsync(options);
            // Console.WriteLine($"Sent message with ID: {messageResponse.Id}");
            return "";
        }
    }
}