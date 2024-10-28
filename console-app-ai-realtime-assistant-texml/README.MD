# Speech Assistant with Telnyx Voice and the OpenAI Realtime API (C#)

This application demonstrates how to use C#, [Telnyx Voice](https://developers.telnyx.com/docs/voice/programmable-voice/get-started) and [TeXML](https://developers.telnyx.com/docs/voice/programmable-voice/texml-setup), and [OpenAI's Realtime API](https://platform.openai.com/docs/) to make a phone call to speak with an AI Assistant.

The application opens WebSocket connections with the OpenAI Realtime API and Telnyx, and sends voice audio between them to enable a two-way conversation.

This application uses the following Telnyx products in conjunction with OpenAI's Realtime API:

- Voice (TeXML, WebRTC)
- Phone Numbers

## Prerequisites

To use the app, you will need:

- **A Telnyx account.** You can [sign up for free](https://telnyx.com/sign-up).
- **.NET 6.0 SDK or later** installed on your machine. You can download it from [here](https://dotnet.microsoft.com/download/dotnet/6.0).
- **A Telnyx number with _Voice_ capabilities.** You can find [a walkthrough to purchase a phone number here](https://support.telnyx.com/en/articles/4380325-search-and-buy-numbers).
- **An OpenAI account and an OpenAI API Key.** You can [sign up here](https://platform.openai.com/).
- **OpenAI Realtime API access.**
- **An `texml.xml` file** located in the base directory of your application, containing the TeXML instructions.

## Local Setup Overview

This project can be summarized by the following steps:

1. **Set up port forwarding or use a tunneling service:**

   - Manually configure your router to port forward (not recommended due to security concerns).
   - Use a tunneling provider like:
     - [Ngrok](https://ngrok.com/).
     - [Expose](https://expose.dev/).
     - [Cloudflare Tunnel](https://developers.cloudflare.com/cloudflare-one/connections/connect-apps/run-tunnel/).

   This guide will demonstrate setting up this project using ngrok, but most tunneling providers will suffice by providing a secure, encrypted connection between your local machine and the internet. This is how your application will be able to communicate with the Telnyx and OpenAI ecosystems.

2. **Set up your Telnyx account:**

   - Purchase a [phone number with Voice capabilities](https://support.telnyx.com/en/articles/4380325-search-and-buy-numbers).
   - Configure a TeXML application in your Telnyx Mission Control Portal.

3. **Set up environment variables:**

   - Create an `.env` file or set environment variables for `OPENAI_API_KEY` and `PORT` as required.

4. **Install dependencies and packages:**

   - Install the necessary NuGet packages for your C# project.

5. **Run the project!**

### Open an ngrok Tunnel

Port forwarding is a networking technique that allows devices on a private network (LAN) to be accessed by devices on an external network (WAN) by directing incoming traffic from a specified port on the WAN to a specific device or service within the LAN. However, this can expose your network to security risks if not configured properly.

To avoid these risks, we can use a tunneling service like ngrok. It provides a secure, encrypted connection between your local machine and the internet without having to modify your router's configuration.

#### Install ngrok

If you haven't already installed ngrok, you can download it from [here](https://ngrok.com/download).

#### Run ngrok

Open a terminal and run:

```bash
ngrok http 8000
```

You should see output similar to:

```
ngrok by @inconshreveable

Session Status                online
Account                       Your Name (Plan: Free)
Version                       2.x.x
Region                        United States (us)
Web Interface                 http://127.0.0.1:4040
Forwarding                    http://abcd1234.ngrok.io -> http://localhost:8000
Forwarding                    https://abcd1234.ngrok.io -> http://localhost:8000

Connections                   ttl     opn     rt1.   rt5     p50     p90
                              0       0       0.00    0.00    0.00    0.00
```

Copy the `Forwarding` URL that starts with `https://`. It will look something like `https://abcd1234.ngrok.io`.

> **Note:** The port `8000` corresponds to the default port configured in the application. If you override the `PORT` variable in your application, you will need to update the ngrok command accordingly.

### Configure Telnyx with the ngrok URL

Once you've set up ngrok or another tunneling service, we need to configure Telnyx to route calls to your application.

#### Create a TeXML Application in Telnyx

1. Log in to your [Telnyx Mission Control Portal](https://portal.telnyx.com/).
2. Navigate to **"Call Control"** > **"TeXML Applications"**.
3. Click **"Add TeXML Application"**.
4. In the **"App Info"** section, provide a name for your application.
5. In the **"TeXML URL"** field, paste the ngrok forwarding address you copied earlier and append `/inbound` to it. For example: `https://abcd1234.ngrok.io/inbound`.
6. Ensure the **"HTTP Method"** is set to **POST**.
7. Click **"Save"**.

#### Assign the TeXML Application to Your Phone Number

1. Navigate to **"Numbers"** > **"My Numbers"**.
2. Click on the phone number you purchased.
3. In the **"Voice Settings"** section, set the **"Connection Type"** to **"TeXML"**.
4. Select the TeXML application you just created from the dropdown.
5. Click **"Save"**.

### Set Up Environment Variables

The application requires environment variables to function correctly. You can set them in your operating system's environment or create an `.env` file in the root directory of your project.

```dotenv
OPENAI_API_KEY=your_openai_api_key_here
PORT=8000
```

Make sure to replace `your_openai_api_key_here` with your actual OpenAI API key.

### Install Dependencies

Ensure you have the .NET 6.0 SDK installed. You can check by running:

```bash
dotnet --version
```

If not installed, download it from [here](https://dotnet.microsoft.com/download/dotnet/6.0).

Create a new C# project or use your existing project. Install the following NuGet packages:

- `Microsoft.AspNetCore.WebSockets`
- `Microsoft.Extensions.Configuration`
- `System.Text.Json`

You can install them using the .NET CLI:

```bash
dotnet add package Microsoft.AspNetCore.WebSockets
dotnet add package Microsoft.Extensions.Configuration
dotnet add package System.Text.Json
```

### Place the `texml.xml` File

Ensure that you have a `texml.xml` file in the base directory of your application. This file contains the TeXML instructions for handling calls. It should include a `{host}` placeholder that will be replaced with the actual host in your application.

**Example `texml.xml` content:**

```xml
<Response>
    <StartStream name="media-stream" url="wss://{host}/media-stream" />
</Response>
```

### Run the App

Build and run the application using the .NET CLI:

```bash
dotnet run
```

The server should start and listen on the specified port, outputting:

```
Now listening on: http://0.0.0.0:8000
Application started. Press Ctrl+C to shut down.
```

### Test the App

Now you should be able to call your Telnyx phone number and have a friendly AI assistant answer and help with your requests!

## Notes

- **ngrok URL Persistence:** Keep in mind that each time you run the `ngrok http` command, a new URL will be generated. You'll need to update your TeXML application's URL in the Telnyx portal with the new ngrok URL each time it changes.

- **Security Considerations:** Remember that exposing your local server to the internet can have security implications. Ensure you trust the code you are running and monitor the server's activity.

- **OpenAI Realtime API Access:** Ensure that your OpenAI account has access to the Realtime API, and that your API key has the necessary permissions.

- **Debugging:** If you encounter any issues, check the console output for error messages. The application includes logging to help you diagnose problems.

## Conclusion

This C# application demonstrates how to integrate Telnyx Voice services with the OpenAI Realtime API to create a voice-based AI assistant accessible via phone calls. By following the steps outlined above, you can set up the application, configure the necessary services, and start interacting with your AI assistant over the phone.

If you have any questions or need further assistance, feel free to reach out to the community or consult the official documentation for Telnyx and OpenAI.

**Happy Coding!**
