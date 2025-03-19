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
                    // Step 3: Send DNSLookup request (Type and Name)
                    DNSRecordRequest dnsRequest = new DNSRecordRequest
                    {
                        Type = "A",    // Example: "A" record for IPv4 address
                        Name = "www.outlook.com"  // Example DNS Name
                    };

                    string dnsRequestJson = JsonSerializer.Serialize(dnsRequest);
                    byte[] dnsRequestBytes = Encoding.UTF8.GetBytes(dnsRequestJson);
                    udpClient.Send(dnsRequestBytes, dnsRequestBytes.Length, serverEndpoint);
                    Console.WriteLine($"Sent DNSLookup request to server: {dnsRequestJson}");

                    // Step 4: Wait for DNSLookupReply from the server
                    receivedBytes = udpClient.Receive(ref serverEndpoint);
                    serverResponse = Encoding.UTF8.GetString(receivedBytes);
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
                    string ackMessage = "ACK";
                    byte[] ackBytes = Encoding.UTF8.GetBytes(ackMessage);
                    udpClient.Send(ackBytes, ackBytes.Length, serverEndpoint);
                    Console.WriteLine($"Sent acknowledgment to server: {ackMessage}");

                    // Step 6: Wait for End message and close
                    receivedBytes = udpClient.Receive(ref serverEndpoint);
                    serverResponse = Encoding.UTF8.GetString(receivedBytes);
                    Console.WriteLine($"Received from server: {serverResponse}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}
