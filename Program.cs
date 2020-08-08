using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using IotDirector.Mqtt;
using AppSettings = IotDirector.Settings.Settings;

namespace IotDirector
{
    class Program
    {
        private static AppSettings ReadSettings()
        {
            using var reader = new StreamReader("settings.json");
            var document = JsonDocument.Parse(reader.ReadToEnd());
            
            return new Settings.Settings(document);
        }
        
        static void Main(string[] args)
        {
            var ctSource= new CancellationTokenSource();
            
            var settings = ReadSettings();
            var server = new TcpListener(IPAddress.Any, settings.ListenPort);
            var mqttClient = new HaMqttClient(settings, ctSource.Token);
            
            server.Start();

            Console.CancelKeyPress += delegate
            {
                ctSource.Cancel();
                server.Server.Shutdown(SocketShutdown.Both);
                server.Server.Close();
                server.Stop();
                
                Console.WriteLine("Server shutdown.");
            };

            try
            {
                while (true)
                {
                    var client = server.AcceptTcpClient();
                    var connection = new Connection.Connection(settings, client, mqttClient, ctSource.Token);
                    
                    connection.Start();
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine($"SocketException: {e.Message}");
                
                ctSource.Cancel();
                server.Server.Shutdown(SocketShutdown.Both);
                server.Server.Close();
                server.Stop();
            }
        }
    }
}