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
    //TODO: [Deserialize Setting.json]
    static string configFile = @"../Setting.json";
    static string configContent = File.ReadAllText(configFile);
    static Setting? setting = JsonSerializer.Deserialize<Setting>(configContent);

    public static void start()
    {
        IPEndPoint serverEndpoint = new IPEndPoint(IPAddress.Parse(setting!.ServerIPAddress!), setting.ServerPortNumber);
        IPEndPoint clientEndpoint = new IPEndPoint(IPAddress.Parse(setting.ClientIPAddress!), setting.ClientPortNumber);
        using (UdpClient udpClient = new UdpClient(clientEndpoint))
        {
            try
            {
                // Step 1: Send HELLO message to the server
                string helloMessage = "HELLO";
                byte[] helloBytes = Encoding.UTF8.GetBytes(helloMessage);
                udpClient.Send(helloBytes, helloBytes.Length, serverEndpoint);
                Console.WriteLine($"Sent: {helloMessage}");

                // Step 2: Wait for the WELCOME message from the server
                byte[] receivedBytes = udpClient.Receive(ref serverEndpoint);
                string serverResponse = Encoding.UTF8.GetString(receivedBytes);
                Console.WriteLine($"Received from server: {serverResponse}");

                if (serverResponse == "WELCOME")
                {
                    // Step 3: Send multiple DNSLookup requests
                    List<DNSRecordRequest> dnsRequests = new List<DNSRecordRequest>
                {
                    new DNSRecordRequest { Type = "A", Name = "www.outlook.com" },   // ✅ Correct
                    new DNSRecordRequest { Type = "MX", Name = "example.com" },      // ✅ Correct
                    new DNSRecordRequest { Type = "A", Name = "www.unknown.com" },   // ❌ Incorrect
                    new DNSRecordRequest { Type = "TXT", Name = "random.example" }   // ❌ Incorrect
                };

                    foreach (var dnsRequest in dnsRequests)
                    {
                        string dnsRequestJson = JsonSerializer.Serialize(dnsRequest);
                        SendMessage(udpClient, serverEndpoint, dnsRequestJson);
                        Console.WriteLine($"Sent DNSLookup request to server: {dnsRequestJson}");

                        // Step 4: Wait for DNSLookupReply from the server
                        serverResponse = ReceiveMessage(udpClient, ref serverEndpoint);
                        Console.WriteLine($"Received from server: {serverResponse}");

                        if (serverResponse.StartsWith("Error"))
                        {
                            Console.WriteLine("Error: DNS record not found");
                        }
                        else
                        {
                            // DNS record found, display it
                            Console.WriteLine($"DNS Record found: {serverResponse}");
                        }

                        // Step 5: Send acknowledgment (ACK) to server
                        SendMessage(udpClient, serverEndpoint, "ACK");
                    }

                    // Step 6: Wait for End message and close
                    serverResponse = ReceiveMessage(udpClient, ref serverEndpoint);
                    Console.WriteLine($"Received from server: {serverResponse}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }

    // Helper function to send messages
    private static void SendMessage(UdpClient client, IPEndPoint endpoint, string message)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(message);
        client.Send(bytes, bytes.Length, endpoint);
    }

    // Helper function to receive messages
    private static string ReceiveMessage(UdpClient client, ref IPEndPoint endpoint)
    {
        byte[] receivedBytes = client.Receive(ref endpoint);
        return Encoding.UTF8.GetString(receivedBytes);
    }
}