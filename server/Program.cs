using System;
using System.Data;
using System.Data.SqlTypes;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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

public class DNSRecord
{
    public string Type { get; set; }
    public string Name { get; set; }
    public string Value { get; set; }
    public int? Priority { get; set; }
    public int TTL { get; set; }
}

class ServerUDP
{
    static string configFile = @"../Setting.json";
    static string dnsFile = @"../DNSrecords.json";
    static Setting? setting;
    static List<DNSRecord> dnsRecords = new List<DNSRecord>();

    public static void start()
    {
        LoadSettings();
        LoadDNSRecords();
        UdpClient server = CreateSocket();
        HandleClient(server);
        server.Close();
    }

    static void LoadSettings()
    {
        string configContent = File.ReadAllText(configFile);
        setting = JsonSerializer.Deserialize<Setting>(configContent);
    }

    static void LoadDNSRecords()
    {
        string dnsFile = @"DNSrecords.json"; // <== Corrected path!

        if (!File.Exists(dnsFile))
        {
            Console.WriteLine($"[ERROR] {dnsFile} not found. Creating a default file...");
            File.WriteAllText(dnsFile, "[]"); // Prevents crashing
        }

        string dnsContent = File.ReadAllText(dnsFile);
        dnsRecords = JsonSerializer.Deserialize<List<DNSRecord>>(dnsContent) ?? new List<DNSRecord>();

        Console.WriteLine($"[INFO] Loaded {dnsRecords.Count} DNS records.");
    }




    static UdpClient CreateSocket()
    {
        UdpClient server = new UdpClient(setting.ServerPortNumber);
        return server;
    }

    static void HandleClient(UdpClient server)
    {
        while (true)
        {
            Message message = ReceiveMessage(server);
            Console.WriteLine($"[SERVER] Received {message.MsgType}");

            switch (message.MsgType)
            {
                case MessageType.Hello:
                    Console.WriteLine("[SERVER] Sending Welcome");
                    SendMessage(server, new Message { MsgId = message.MsgId, MsgType = MessageType.Welcome });
                    break;

                case MessageType.DNSLookup:
                    Console.WriteLine($"[SERVER] Looking up: {message.Content}");
                    ProcessDNSLookup(server, message);
                    break;

                case MessageType.Ack:
                    Console.WriteLine("[SERVER] Received Acknowledgment.");
                    break;

                case MessageType.End:
                    Console.WriteLine("[SERVER] Client Disconnected.");
                    return;

                default:
                    Console.WriteLine("[SERVER] Unknown message type.");
                    break;
            }
        }
    }



    static void ProcessDNSLookup(UdpClient server, Message request)
    {
        string domain = request.Content?.ToString();
        Console.WriteLine($"[SERVER] Searching DNS for {domain}");

        if (string.IsNullOrEmpty(domain))
        {
            Console.WriteLine("[SERVER] ERROR: Received an empty domain request.");
            SendMessage(server, new Message { MsgId = request.MsgId, MsgType = MessageType.Error, Content = "Invalid request" });
            return;
        }

        var record = dnsRecords.FirstOrDefault(r => r.Name == domain);

        if (record != null)
        {
            Console.WriteLine($"[SERVER] Found: {record.Value}");
            SendMessage(server, new Message { MsgId = request.MsgId, MsgType = MessageType.DNSLookupReply, Content = record });
        }
        else
        {
            Console.WriteLine($"[SERVER] ERROR: {domain} not found.");
            SendMessage(server, new Message { MsgId = request.MsgId, MsgType = MessageType.Error, Content = "Domain not found" });
        }
    }



    static void SendMessage(UdpClient server, Message message)
    {
        byte[] data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        server.Send(data, data.Length, new IPEndPoint(IPAddress.Parse(setting.ClientIPAddress), setting.ClientPortNumber));
    }

    static Message ReceiveMessage(UdpClient server)
    {
        try
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = server.Receive(ref remoteEP);
            return JsonSerializer.Deserialize<Message>(Encoding.UTF8.GetString(data));
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"[ERROR] Connection issue: {ex.Message}");
            return new Message { MsgType = MessageType.Error, Content = "Connection lost" };
        }
    }
}

