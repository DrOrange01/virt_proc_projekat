using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Client
{
    public class SolarServiceProxy : IDisposable
    {
        private ChannelFactory<ISolarService> factory;
        private ISolarService channel;
        private bool disposed = false;

        public SolarServiceProxy(string endpointName = "SolarServiceEndpoint")
        {
            factory = new ChannelFactory<ISolarService>(endpointName);
            channel = factory.CreateChannel();
        }

        public ServerAck StartSession(PvMeta meta)
        {
            try
            {
                return channel.StartSession(meta);
            }
            catch (FaultException<string> fex)
            {
                Console.WriteLine($"Service fault: {fex.Detail}");
                throw;
            }
            catch (CommunicationException cex)
            {
                Console.WriteLine($"Communication error: {cex.Message}");
                throw;
            }
        }

        public ServerAck PushSample(PvSample sample)
        {
            try
            {
                return channel.PushSample(sample);
            }
            catch (FaultException<string> fex)
            {
                Console.WriteLine($"Service fault: {fex.Detail}");
                throw;
            }
            catch (CommunicationException cex)
            {
                Console.WriteLine($"Communication error: {cex.Message}");
                throw;
            }
        }

        public ServerAck EndSession()
        {
            try
            {
                return channel.EndSession();
            }
            catch (FaultException<string> fex)
            {
                Console.WriteLine($"Service fault: {fex.Detail}");
                throw;
            }
            catch (CommunicationException cex)
            {
                Console.WriteLine($"Communication error: {cex.Message}");
                throw;
            }
        }

        public List<string> GetWarnings()
        {
            try
            {
                return channel.GetWarnings();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting warnings: {ex.Message}");
                return new List<string>();
            }
        }

        public void Dispose()
        {
            if (!disposed)
            {
                try
                {
                    if (channel != null)
                    {
                        var comm = channel as ICommunicationObject;
                        if (comm != null)
                        {
                            if (comm.State == CommunicationState.Faulted)
                                comm.Abort();
                            else
                                comm.Close();
                        }
                    }
                    factory?.Close();
                }
                catch
                {
                    factory?.Abort();
                }
                disposed = true;
            }
        }
    }
}
