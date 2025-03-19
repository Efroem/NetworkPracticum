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

// SendTo();
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
    static string configContent = File.ReadAllText(configFile);
    static Setting? setting = JsonSerializer.Deserialize<Setting>(configContent);

    public static void start()
    {
        if (setting == null)
        {
            Console.WriteLine("Failed to load settings.");
            return;
        }

        IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse(setting.ServerIPAddress), setting.ServerPortNumber);
        using Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        try
        {
            // Create Hello Message
            Message helloMessage = new Message
            {
                MsgId = new Random().Next(1, 10000),
                MsgType = MessageType.Hello,
                Content = "Hello from client"
            };

            byte[] sendBuffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(helloMessage));
            socket.SendTo(sendBuffer, serverEndPoint);
            Console.WriteLine("[CLIENT] Sent: " + JsonSerializer.Serialize(helloMessage));

            // Receive Welcome Response
            byte[] receiveBuffer = new byte[1024];
            EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            int receivedBytes = socket.ReceiveFrom(receiveBuffer, ref remoteEndPoint);
            string receivedData = Encoding.UTF8.GetString(receiveBuffer, 0, receivedBytes);
            Message? receivedMessage = JsonSerializer.Deserialize<Message>(receivedData);

            if (receivedMessage != null && receivedMessage.MsgType == MessageType.Welcome)
            {
                Console.WriteLine("[CLIENT] Received: " + receivedData);
            }
            else
            {
                Console.WriteLine("[CLIENT] Unexpected response from server: " + receivedData);
                return;
            }

            // Step 3: Send Multiple DNS Lookup Messages
            Message[] dnsLookupMessages = new Message[]
            {
                new Message { MsgId = new Random().Next(1, 10000), MsgType = MessageType.DNSLookup, Content = new { Type = "A", Name = "www.test.com" } },
                new Message { MsgId = new Random().Next(1, 10000), MsgType = MessageType.DNSLookup, Content = new { Type = "MX", Name = "example.com" } },
                new Message { MsgId = new Random().Next(1, 10000), MsgType = MessageType.DNSLookup, Content = new { Type = "A", Name = "www.unknown.com" } },
                new Message { MsgId = new Random().Next(1, 10000), MsgType = MessageType.DNSLookup, Content = new { Type = "CNAME", Name = "invalid.domain" } }
            };

            foreach (var dnsLookupMessage in dnsLookupMessages)
            {
                byte[] dnsBuffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(dnsLookupMessage));
                socket.SendTo(dnsBuffer, serverEndPoint);
                Console.WriteLine("[CLIENT] Sent DNS Lookup: " + JsonSerializer.Serialize(dnsLookupMessage));

                // Receive DNS Lookup Reply or Error
                receiveBuffer = new byte[1024];
                receivedBytes = socket.ReceiveFrom(receiveBuffer, ref remoteEndPoint);
                receivedData = Encoding.UTF8.GetString(receiveBuffer, 0, receivedBytes);
                receivedMessage = JsonSerializer.Deserialize<Message>(receivedData);

                if (receivedMessage != null && receivedMessage.MsgType == MessageType.DNSLookupReply)
                {
                    Console.WriteLine("[CLIENT] Received DNSLookupReply: " + receivedData);

                    // Send Acknowledgment (Ack) Message
                    Message ackMessage = new Message
                    {
                        MsgId = new Random().Next(1, 10000),
                        MsgType = MessageType.Ack,
                        Content = dnsLookupMessage.MsgId  // The MsgId of the original DNSLookup request
                    };

                    byte[] ackBuffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(ackMessage));
                    socket.SendTo(ackBuffer, serverEndPoint);
                    Console.WriteLine("[CLIENT] Sent Ack: " + JsonSerializer.Serialize(ackMessage));
                }
                else if (receivedMessage != null && receivedMessage.MsgType == MessageType.Error)
                {
                    Console.WriteLine("[CLIENT] Received Error: " + receivedData);
                }
                else
                {
                    Console.WriteLine("[CLIENT] Unexpected response from server: " + receivedData);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[CLIENT] Error: " + ex.Message);
        }
    }
}
