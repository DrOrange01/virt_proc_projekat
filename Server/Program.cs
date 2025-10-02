using Server.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Services;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Service
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Solar Panel Data Service ===\n");

            var service = new SolarService();

            service.OnTransferStarted += (s, e) =>
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Transfer started.");

            service.OnSampleReceived += (s, e) =>
            {
                if (e.TotalReceived % 10 == 0)
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Received: {e.TotalReceived} samples");
            };

            service.OnTransferCompleted += (s, e) =>
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Transfer completed.");

            service.OnWarningRaised += (s, e) =>
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] WARNING - {e.WarningType}: {e.Message}");

            using (var host = new ServiceHost(service))
            {
                try
                {
                    host.Open();
                    Console.WriteLine("Service is running at net.tcp://localhost:4000/SolarService");
                    Console.WriteLine("Press ENTER to stop...\n");
                    Console.ReadLine();
                    host.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: {ex.Message}");
                    host.Abort();
                }
            }

            Console.WriteLine("Service stopped.");
            Console.ReadKey();
        }
    }
}
