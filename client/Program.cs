using System.Collections.Immutable;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using LibData;

class Program
{
    static void Main(string[] args)
    {
        ClientUDP.start();
    }
}

public class Setting
{
    public int ServerPortNumber { get; set; }
    public string? ServerIPAddress { get; set; }
    public int ClientPortNumber { get; set; }
    public string? ClientIPAddress { get; set; }
}

class ClientUDP
{
    static string configFile = "../Setting.json";
    static Setting? setting;

    public static void start()
    {
        if (!LoadSettings())
            return;

        if (setting == null || string.IsNullOrEmpty(setting.ServerIPAddress))
        {
            throw new InvalidOperationException("[CLIENT] Invalid settings: ServerIPAddress is null or empty.");
        }

        IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse(setting.ServerIPAddress), setting.ServerPortNumber);
        Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        try
        {
            if (!PerformHandshake(socket, serverEndPoint))
                return;

            SendDNSLookups(socket, serverEndPoint);

            WaitForEndOrSendFallback(socket, serverEndPoint);
        }
        catch (Exception ex)
        {
            Console.WriteLine("[CLIENT] Error: " + ex.Message);
        }
    }

    private static bool LoadSettings()
    {
        try
        {
            string configContent = File.ReadAllText(configFile);
            setting = JsonSerializer.Deserialize<Setting>(configContent);

            if (setting == null || string.IsNullOrEmpty(setting.ServerIPAddress))
                throw new Exception("Invalid or missing configuration.");

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine("[CLIENT] Failed to load settings: " + ex.Message);
            return false;
        }
    }

    private static bool PerformHandshake(Socket socket, IPEndPoint serverEndPoint)
    {
        try
        {
            Message helloMessage = new Message
            {
                MsgId = new Random().Next(1, 10000),
                MsgType = MessageType.Hello,
                Content = "Hello from client"
            };

            SendMessage(socket, helloMessage, serverEndPoint, "[CLIENT] Sent");

            Message? received = ReceiveMessage(socket, "[CLIENT] Waiting for Welcome");

            if (received != null && received.MsgType == MessageType.Welcome)
            {
                Console.WriteLine("[CLIENT] Received: " + JsonSerializer.Serialize(received));
                return true;
            }

            Console.WriteLine("[CLIENT] Unexpected response during handshake: " + JsonSerializer.Serialize(received));
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine("[CLIENT] Handshake failed: " + ex.Message);
            return false;
        }
    }

    private static void SendDNSLookups(Socket socket, IPEndPoint serverEndPoint)
    {
        Message[] dnsLookupMessages = new Message[]
        {
            new Message { MsgId = new Random().Next(1, 10000), MsgType = MessageType.DNSLookup, Content = new { Type = "A", Name = "www.test.com" } }, //correct
            new Message { MsgId = new Random().Next(1, 10000), MsgType = MessageType.DNSLookup, Content = new { Type = "MX", Name = "example.com" } }, //correct
            new Message { MsgId = new Random().Next(1, 10000), MsgType = MessageType.DNSLookup, Content = new { Type = "A", Name = "www.unknown.com" } }, //incorrect
            new Message { MsgId = new Random().Next(1, 10000), MsgType = MessageType.DNSLookup, Content = new { Type = "CNAME", Name = "invalid.domain" } } //incorrect
        };

        foreach (var message in dnsLookupMessages)
        {
            SendMessage(socket, message, serverEndPoint, "[CLIENT] Sent DNS Lookup");

            Message? reply = ReceiveMessage(socket, "[CLIENT] Waiting for DNS response");

            if (reply != null && reply.MsgType == MessageType.DNSLookupReply)
            {
                Console.WriteLine("[CLIENT] Received DNSLookupReply: " + JsonSerializer.Serialize(reply));

                Message ackMessage = new Message
                {
                    MsgId = new Random().Next(1, 10000),
                    MsgType = MessageType.Ack,
                    Content = message.MsgId
                };

                SendMessage(socket, ackMessage, serverEndPoint, "[CLIENT] Sent Ack");
            }
            else if (reply != null && reply.MsgType == MessageType.Error)
            {
                Console.WriteLine("[CLIENT] Received Error: " + JsonSerializer.Serialize(reply));
            }
            else
            {
                Console.WriteLine("[CLIENT] Unexpected response: " + JsonSerializer.Serialize(reply));
            }
        }
    }

    private static void WaitForEndOrSendFallback(Socket socket, IPEndPoint serverEndPoint)
    {
        EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        byte[] buffer = new byte[1024];
        socket.ReceiveTimeout = 5000;

        try
        {
            int receivedBytes = socket.ReceiveFrom(buffer, ref remoteEndPoint);
            string receivedData = Encoding.UTF8.GetString(buffer, 0, receivedBytes);
            Message? message = JsonSerializer.Deserialize<Message>(receivedData);

            if (message != null && message.MsgType == MessageType.End)
            {
                Console.WriteLine("[CLIENT] Received END message from server. Terminating client.");
            }
            else
            {
                Console.WriteLine("[CLIENT] Received unexpected message while waiting for END: " + receivedData);
            }
        }
        catch (SocketException ex)
        {
            if (ex.SocketErrorCode == SocketError.TimedOut)
            {
                Console.WriteLine("[CLIENT] Timeout: No END message received. Sending END manually.");

                Message endMessage = new Message
                {
                    MsgId = new Random().Next(1, 10000),
                    MsgType = MessageType.End,
                    Content = "Client manually ended session"
                };

                SendMessage(socket, endMessage, serverEndPoint, "[CLIENT] Sent End");
            }
            else
            {
                Console.WriteLine("[CLIENT] Socket error while waiting for END: " + ex.Message);
            }
        }
    }



    private static void SendMessage(Socket socket, Message message, EndPoint endpoint, string logPrefix)
    {
        byte[] buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        socket.SendTo(buffer, endpoint);
        Console.WriteLine($"{logPrefix}: {JsonSerializer.Serialize(message)}");
    }

    private static Message? ReceiveMessage(Socket socket, string logPrefix)
    {
        try
        {
            byte[] buffer = new byte[1024];
            EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            int receivedBytes = socket.ReceiveFrom(buffer, ref remoteEndPoint);
            string receivedData = Encoding.UTF8.GetString(buffer, 0, receivedBytes);

            return JsonSerializer.Deserialize<Message>(receivedData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{logPrefix} - Error: {ex.Message}");
            return null;
        }
    }
}
