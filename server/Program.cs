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
    static string dnsFile = "./DNSrecords.json";
    static string configContent = File.ReadAllText(configFile);
    static Setting? setting = JsonSerializer.Deserialize<Setting>(configContent);

    public static List<DNSRecord> LoadDNSRecords()
    {
        if (File.Exists(dnsFile))
        {
            string json = File.ReadAllText(dnsFile);
            return JsonSerializer.Deserialize<List<DNSRecord>>(json) ?? new List<DNSRecord>();
        }
        else
        {
            Console.WriteLine($"Error: {dnsFile} not found!");
            return new List<DNSRecord>();
        }
    }

    public static void start()
    {
        static void DNSRecordPrint()
        {
            List<DNSRecord> dnsRecords = ServerUDP.LoadDNSRecords();
            if (dnsRecords.Count == 0)
            {
                Console.WriteLine("No DNS records found");
                return;
            }
            foreach (var record in dnsRecords)
            {
                Console.WriteLine($"DNS Record: {record.Name} -> {record.Value}");
            }
        }

        DNSRecordPrint();

        IPEndPoint serverEndpoint = new IPEndPoint(IPAddress.Parse(setting!.ServerIPAddress!), setting.ServerPortNumber);
        using (UdpClient udpServer = new UdpClient(serverEndpoint))
        {
            Console.WriteLine($"Server is listening on {serverEndpoint}");

            try
            {
                IPEndPoint clientEndpoint = new IPEndPoint(IPAddress.Any, 0);

                // Step 1: Receive HELLO message from the client
                byte[] receivedBytes = udpServer.Receive(ref clientEndpoint);
                string receivedMessage = Encoding.UTF8.GetString(receivedBytes);
                Console.WriteLine($"Received from client: {receivedMessage}");

                if (receivedMessage == "HELLO")
                {
                    // Send "WELCOME" to the client
                    string welcomeMessage = "WELCOME";
                    byte[] welcomeBytes = Encoding.UTF8.GetBytes(welcomeMessage);
                    udpServer.Send(welcomeBytes, welcomeBytes.Length, clientEndpoint);
                    Console.WriteLine($"Sent to client: {welcomeMessage}");
                }

                // Step 2: Process multiple DNS lookup requests before sending "End"
                while (true)
                {
                    // Receive the DNSLookup request
                    receivedBytes = udpServer.Receive(ref clientEndpoint);
                    string dnsLookupMessage = Encoding.UTF8.GetString(receivedBytes);
                    Console.WriteLine($"Received DNSLookup from client: {dnsLookupMessage}");

                    if (dnsLookupMessage == "End") break; // Stop when "End" is received

                    // Deserialize the DNSLookup message (assuming it's JSON)
                    var dnsRequest = JsonSerializer.Deserialize<DNSRecord>(dnsLookupMessage);
                    string response = "Error: DNS record not found";

                    if (dnsRequest != null)
                    {
                        // Query the DNSRecord in the JSON file
                        List<DNSRecord> dnsRecords = LoadDNSRecords();
                        DNSRecord? foundRecord = dnsRecords.Find(record => record.Name == dnsRequest.Name && record.Type == dnsRequest.Type);

                        if (foundRecord != null)
                        {
                            response = JsonSerializer.Serialize(foundRecord);
                        }
                    }

                    // Step 3: Send Response (DNSLookupReply or Error)
                    byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                    udpServer.Send(responseBytes, responseBytes.Length, clientEndpoint);
                    Console.WriteLine($"Sent to client: {response}");

                    // Step 4: Receive acknowledgment of correct DNSLookupReply
                    receivedBytes = udpServer.Receive(ref clientEndpoint);
                    string ackMessage = Encoding.UTF8.GetString(receivedBytes);
                    Console.WriteLine($"Received acknowledgment from client: {ackMessage}");

                    // Step 5: Send "End" message to indicate completion
                    string endMessage = "End";
                    byte[] endBytes = Encoding.UTF8.GetBytes(endMessage);
                    udpServer.Send(endBytes, endBytes.Length, clientEndpoint);
                    Console.WriteLine($"Sent to client: {endMessage}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}
