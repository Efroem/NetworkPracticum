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
    static string configContent = File.ReadAllText(configFile);
    static Setting? setting = JsonSerializer.Deserialize<Setting>(configContent);
    static string dnsRecordsFile = "./DNSrecords.json";
    static List<DNSRecord>? dnsRecords;
    static int ackCount = 0;
    static int expectedAcks = 2;

    public static void start()
    {
        if (setting == null)
        {
            Console.WriteLine("Failed to load settings.");
            return;
        }

        if (File.Exists(dnsRecordsFile))
        {
            string dnsContent = File.ReadAllText(dnsRecordsFile);
            dnsRecords = JsonSerializer.Deserialize<List<DNSRecord>>(dnsContent);
        }
        else
        {
            Console.WriteLine("[SERVER] DNS records file not found.");
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

                        if (receivedMessage.MsgType == MessageType.Hello)
                        {
                            Message welcomeMessage = new Message
                            {
                                MsgId = receivedMessage.MsgId + 1,
                                MsgType = MessageType.Welcome,
                                Content = "Welcome from server"
                            };

                            byte[] sendBuffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(welcomeMessage));
                            socket.SendTo(sendBuffer, clientEndPoint);
                            Console.WriteLine("[SERVER] Sent: " + JsonSerializer.Serialize(welcomeMessage));
                        }
                        else if (receivedMessage.MsgType == MessageType.DNSLookup)
                        {
                            var lookupData = JsonSerializer.Deserialize<JsonElement>(receivedMessage.Content.ToString());
                            string type = lookupData.GetProperty("Type").GetString();
                            string name = lookupData.GetProperty("Name").GetString();

                            var record = dnsRecords?.Find(r => r.Type == type && r.Name == name);

                            if (record != null)
                            {
                                Message dnsReplyMessage = new Message
                                {
                                    MsgId = receivedMessage.MsgId,
                                    MsgType = MessageType.DNSLookupReply,
                                    Content = record
                                };

                                byte[] sendBuffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(dnsReplyMessage));
                                socket.SendTo(sendBuffer, clientEndPoint);
                                Console.WriteLine("[SERVER] Sent DNSLookupReply: " + JsonSerializer.Serialize(dnsReplyMessage));
                            }
                            else
                            {
                                Message errorMessage = new Message
                                {
                                    MsgId = receivedMessage.MsgId,
                                    MsgType = MessageType.Error,
                                    Content = "Domain not found"
                                };

                                byte[] sendBuffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(errorMessage));
                                socket.SendTo(sendBuffer, clientEndPoint);
                                Console.WriteLine("[SERVER] Sent Error: " + JsonSerializer.Serialize(errorMessage));
                            }
                        }
                        else if (receivedMessage.MsgType == MessageType.Ack)
                        {
                            Console.WriteLine("[SERVER] Received Ack for MsgId: " + receivedMessage.Content);
                            ackCount++;

                            if (ackCount >= expectedAcks)
                            {
                                Message endMessage = new Message
                                {
                                    MsgId = new Random().Next(1, 10000),
                                    MsgType = MessageType.End,
                                    Content = "End of DNS Lookup process"
                                };

                                byte[] sendBuffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(endMessage));
                                socket.SendTo(sendBuffer, clientEndPoint);
                                Console.WriteLine("[SERVER] Sent End Message: " + JsonSerializer.Serialize(endMessage));

                                ackCount = 0;
                            }
                        }
                    }
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode == SocketError.ConnectionReset || ex.SocketErrorCode == SocketError.NetworkDown)
                    {
                        Console.WriteLine("[SERVER] Client disconnected or connection reset. Continuing...");
                        continue;
                    }
                    Console.WriteLine("[SERVER] Socket error: " + ex.Message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[SERVER] Error in receiving or processing message: " + ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[SERVER] Error: " + ex.Message);
        }
    }
}