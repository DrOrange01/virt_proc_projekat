using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Services;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;

namespace Service
{
    internal class Program
    {
        static void Main(string[] args)
        {
            ServiceHost host = new ServiceHost(typeof(LibraryService));
            host.Open();

            Console.WriteLine("Service is running...");
            Console.ReadKey();

            host.Close();
            Console.WriteLine("Service is closed.");
        }
    }
}
