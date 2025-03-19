using System;
using System.Data;
using System.Data.SqlTypes;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LibData;

// ReceiveFrom();
class Program
{
    static void Main(string[] args)
    {
        ServerUDP.start();
    }
}

public class Setting
{
    public int ServerPortNumber { get; set; }
    public string? ServerIPAddress { get; set; }
    public int ClientPortNumber { get; set; }
    public string? ClientIPAddress { get; set; }
}


class ServerUDP
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
            socket.Bind(serverEndPoint);
            Console.WriteLine("[SERVER] Listening on " + setting.ServerIPAddress + ":" + setting.ServerPortNumber);

            while (true)
            {
                byte[] receiveBuffer = new byte[1024];
                EndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);
                int receivedBytes = socket.ReceiveFrom(receiveBuffer, ref clientEndPoint);

                string receivedData = Encoding.UTF8.GetString(receiveBuffer, 0, receivedBytes);
                Message? receivedMessage = JsonSerializer.Deserialize<Message>(receivedData);

                if (receivedMessage != null && receivedMessage.MsgType == MessageType.Hello)
                {
                    Console.WriteLine("[SERVER] Received: " + receivedData);

                    // Send Welcome Response
                    Message welcomeMessage = new Message
                    {
                        MsgId = receivedMessage.MsgId + 1, // Increment MsgId
                        MsgType = MessageType.Welcome,
                        Content = "Welcome from server"
                    };

                    byte[] sendBuffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(welcomeMessage));
                    socket.SendTo(sendBuffer, clientEndPoint);
                    Console.WriteLine("[SERVER] Sent: " + JsonSerializer.Serialize(welcomeMessage));
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[SERVER] Error: " + ex.Message);
        }
    }
}