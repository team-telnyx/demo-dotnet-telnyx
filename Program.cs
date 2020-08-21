using System;
using Telnyx;
using System.Threading.Tasks;

namespace demo_dotnet_telnyx
{
    class Program
    {
        private static string TELNYX_API_KEY = System.Environment.GetEnvironmentVariable("TELNYX_API_KEY");
        static async Task Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            TelnyxConfiguration.SetApiKey(TELNYX_API_KEY);
            MessagingSenderIdService service = new MessagingSenderIdService();
            NewMessagingSenderId options = new NewMessagingSenderId
            {
                From = "+19198675309", // alphanumeric sender id
                To = "+19198675310",
                Text = "Hello, World!"
            };
            MessagingSenderId messageResponse = await service.CreateAsync(options);
            Console.WriteLine(messageResponse.Id);
        }
    }
}
