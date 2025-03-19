using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
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

public class DNSRecordRequest
{
    public string Type { get; set; }
    public string Name { get; set; }
}

class ClientUDP
{
    static string configFile = @"../Setting.json";
    static Setting? setting;

    public static void start()
    {
        LoadSettings();
        UdpClient client = CreateSocket();
        SendHello(client);
        ReceiveWelcome(client);
        ProcessDNSLookups(client);
        ReceiveEnd(client);
        client.Close();
    }

    static void LoadSettings()
    {
        string configContent = File.ReadAllText(configFile);
        setting = JsonSerializer.Deserialize<Setting>(configContent);
    }

    static UdpClient CreateSocket()
    {
        return new UdpClient(setting.ClientPortNumber);
    }

    static void SendHello(UdpClient client)
    {
        Message helloMessage = new Message { MsgId = 1, MsgType = MessageType.Hello };
        SendMessage(client, helloMessage);
    }

    static void ReceiveWelcome(UdpClient client)
    {
        Message welcomeMessage = ReceiveMessage(client);
        if (welcomeMessage.MsgType == MessageType.Welcome)
        {
            Console.WriteLine("Received Welcome from Server.");
        }
    }

    static void ProcessDNSLookups(UdpClient client)
    {
        string[] domains = { "www.test.com", "www.unknown.com" };
        foreach (var domain in domains)
        {
            SendMessage(client, new Message { MsgId = 2, MsgType = MessageType.DNSLookup, Content = domain });
            Message response = ReceiveMessage(client);
            Console.WriteLine($"Received: {response.MsgType} for {domain}");
        }
    }

    static void ReceiveEnd(UdpClient client)
    {
        Message endMessage = ReceiveMessage(client);
        if (endMessage.MsgType == MessageType.End)
        {
            Console.WriteLine("Communication Ended.");
        }
    }

    static void SendMessage(UdpClient client, Message message)
    {
        byte[] data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        client.Send(data, data.Length, new IPEndPoint(IPAddress.Parse(setting.ServerIPAddress), setting.ServerPortNumber));
    }

    static Message ReceiveMessage(UdpClient client)
    {
        try
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = client.Receive(ref remoteEP);
            return JsonSerializer.Deserialize<Message>(Encoding.UTF8.GetString(data));
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"[ERROR] Connection lost: {ex.Message}");
            return new Message { MsgType = MessageType.Error, Content = "Connection lost" };
        }
    }

}