using Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;

namespace Client
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Solar Panel Data Client ===\n");

            var csvPath = ConfigurationManager.AppSettings["CsvPath"] ??
                          @"C:\temp\solar_data.csv";
            var rowLimit = 200;

            if (!File.Exists(csvPath))
            {
                Console.WriteLine($"ERROR: CSV file not found: {csvPath}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            using (var reader = new CsvReader(csvPath))
            using (var proxy = new SolarServiceProxy())
            {
                try
                {
                    Console.WriteLine($"Starting session with file: {Path.GetFileName(csvPath)}");
                    Console.WriteLine($"Row limit: {rowLimit}\n");

                    var meta = new PvMeta
                    {
                        PlantId = "PLANT-001",
                        FileName = Path.GetFileName(csvPath),
                        TotalRows = CountLines(csvPath) - 1, 
                        SchemaVersion = "1.0",
                        RowLimitN = rowLimit,
                        SessionDateUtc = DateTime.UtcNow
                    };

                    var startAck = proxy.StartSession(meta);
                    if (!startAck.Success)
                    {
                        Console.WriteLine($"Failed to start session: {startAck.Message}");
                        return;
                    }

                    Console.WriteLine($"Session started: {startAck.Message}\n");

                    Console.WriteLine("Sending samples...\n");
                    int count = 0;

                    foreach (var sample in reader.ReadSamples(rowLimit))
                    {
                        var ack = proxy.PushSample(sample);
                        count++;

                        if (count % 10 == 0)
                        {
                            Console.WriteLine($"Sent {count}/{rowLimit} samples " +
                                            $"({ack.PercentOfLimit:F1}%)");
                        }

                        if (!ack.Success)
                        {
                            Console.WriteLine($"Warning on row {sample.RowIndex}: {ack.Message}");
                        }
                    }

                    Console.WriteLine($"\nTotal samples sent: {count}\n");

                    var endAck = proxy.EndSession();
                    Console.WriteLine($"Session ended: {endAck.Message}");
                    Console.WriteLine($"Server received: {endAck.ReceivedCount} samples\n");

                    Console.WriteLine("Fetching warnings from server...\n");
                    var warnings = proxy.GetWarnings();

                    if (warnings.Count > 0)
                    {
                        Console.WriteLine($"=== WARNINGS ({warnings.Count}) ===");
                        foreach (var w in warnings)
                            Console.WriteLine($"  • {w}");
                    }
                    else
                    {
                        Console.WriteLine("No warnings generated.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\nERROR: {ex.Message}");
                    Console.WriteLine($"Type: {ex.GetType().Name}");
                    if (ex.InnerException != null)
                        Console.WriteLine($"Inner: {ex.InnerException.Message}");
                }
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        static int CountLines(string path)
        {
            int count = 0;
            using (var r = new StreamReader(path))
            {
                while (r.ReadLine() != null)
                    count++;
            }
            return count;
        }
    }
}
