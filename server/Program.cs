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
    static string dnsRecordsFile = "./DNSrecords.json";

    static Setting? setting;
    static List<DNSRecord>? dnsRecords;

    public static void start()
    {
        if (!LoadSettings() || !LoadDNSRecords()) return;

        IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse(setting.ServerIPAddress), setting.ServerPortNumber);
        using Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        try
        {
            socket.Bind(serverEndPoint);
            Console.WriteLine("[SERVER] Listening on " + setting.ServerIPAddress + ":" + setting.ServerPortNumber);

            while (true)
            {
                ProcessIncomingMessage(socket);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[SERVER] Error: " + ex.Message);
        }
    }

    private static bool LoadSettings()
    {
        try
        {
            string configContent = File.ReadAllText(configFile);
            setting = JsonSerializer.Deserialize<Setting>(configContent);

            if (setting == null)
                throw new Exception("Settings deserialization returned null.");

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine("[SERVER] Failed to load settings: " + ex.Message);
            return false;
        }
    }

    private static bool LoadDNSRecords()
    {
        if (!File.Exists(dnsRecordsFile))
        {
            Console.WriteLine("[SERVER] DNS records file not found.");
            return false;
        }

        try
        {
            string dnsContent = File.ReadAllText(dnsRecordsFile);
            dnsRecords = JsonSerializer.Deserialize<List<DNSRecord>>(dnsContent);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine("[SERVER] Failed to load DNS records: " + ex.Message);
            return false;
        }
    }

    private static void ProcessIncomingMessage(Socket socket)
    {
        try
        {
            byte[] receiveBuffer = new byte[1024];
            EndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);
            int receivedBytes = socket.ReceiveFrom(receiveBuffer, ref clientEndPoint);

            string receivedData = Encoding.UTF8.GetString(receiveBuffer, 0, receivedBytes);
            Message? receivedMessage = JsonSerializer.Deserialize<Message>(receivedData);

            if (receivedMessage != null)
            {
                Console.WriteLine("[SERVER] Received: " + receivedData);
                HandleMessage(socket, receivedMessage, clientEndPoint);
            }
        }
        catch (SocketException ex)
        {
            Console.WriteLine("[SERVER] Socket error: " + ex.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine("[SERVER] Error in receiving or processing message: " + ex.Message);
        }
    }

    private static void HandleMessage(Socket socket, Message receivedMessage, EndPoint clientEndPoint)
    {
        switch (receivedMessage.MsgType)
        {
            case MessageType.Hello:
                SendWelcome(socket, receivedMessage, clientEndPoint);
                break;
            case MessageType.DNSLookup:
                ProcessDNSLookup(socket, receivedMessage, clientEndPoint);
                break;
            case MessageType.Ack:
                HandleAck(socket, receivedMessage, clientEndPoint);
                break;
        }
    }

    private static void SendWelcome(Socket socket, Message helloMessage, EndPoint clientEndPoint)
    {
        Message welcomeMessage = new Message
        {
            MsgId = helloMessage.MsgId + 1,
            MsgType = MessageType.Welcome,
            Content = "Welcome from server"
        };

        SendMessage(socket, welcomeMessage, clientEndPoint, "[SERVER] Sent");
    }

    private static void ProcessDNSLookup(Socket socket, Message lookupMessage, EndPoint clientEndPoint)
    {
        try
        {
            var lookupData = JsonSerializer.Deserialize<JsonElement>(lookupMessage.Content.ToString());
            string? type = lookupData.GetProperty("Type").GetString();
            string? name = lookupData.GetProperty("Name").GetString();

            var record = dnsRecords?.Find(r => r.Type == type && r.Name == name);

            if (record != null)
            {
                Message reply = new Message
                {
                    MsgId = lookupMessage.MsgId,
                    MsgType = MessageType.DNSLookupReply,
                    Content = record
                };
                SendMessage(socket, reply, clientEndPoint, "[SERVER] Sent DNSLookupReply");
            }
            else
            {
                SendError(socket, lookupMessage, clientEndPoint, "Domain not found");
            }
        }
        catch
        {
            SendError(socket, lookupMessage, clientEndPoint, "Invalid DNSLookup request");
        }
    }

    private static void HandleAck(Socket socket, Message ackMessage, EndPoint clientEndPoint)
    {
        Console.WriteLine("[SERVER] Received Ack for MsgId: " + ackMessage.Content);
    }


    private static void SendError(Socket socket, Message originalMessage, EndPoint clientEndPoint, string errorMsg)
    {
        Message error = new Message
        {
            MsgId = originalMessage.MsgId,
            MsgType = MessageType.Error,
            Content = errorMsg
        };
        SendMessage(socket, error, clientEndPoint, "[SERVER] Sent Error");
    }

    private static void SendMessage(Socket socket, Message message, EndPoint clientEndPoint, string logPrefix)
    {
        byte[] buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        socket.SendTo(buffer, clientEndPoint);
        Console.WriteLine($"{logPrefix}: {JsonSerializer.Serialize(message)}");
    }
}
