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

    // TODO: [Read the JSON file and return the list of DNSRecords]
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
        // DNS Record printing
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


        // TODO: [Create a socket and endpoints and bind it to the server IP address and port number]



        // TODO:[Receive and print a received Message from the client]




        // TODO:[Receive and print Hello]



        // TODO:[Send Welcome to the client]


        // TODO:[Receive and print DNSLookup]


        // TODO:[Query the DNSRecord in Json file]

        // TODO:[If found Send DNSLookupReply containing the DNSRecord]



        // TODO:[If not found Send Error]


        // TODO:[Receive Ack about correct DNSLookupReply from the client]


        // TODO:[If no further requests receieved send End to the client]

    }


}