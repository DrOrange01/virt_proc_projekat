using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;

namespace Common
{
    [ServiceContract(SessionMode = SessionMode.NotAllowed)]
    public interface ISolarService
    {
        [OperationContract]
        [FaultContract(typeof(string))]
        ServerAck StartSession(PvMeta meta);

        [OperationContract]
        [FaultContract(typeof(string))]
        ServerAck PushSample(PvSample sample);

        [OperationContract]
        [FaultContract(typeof(string))]
        ServerAck EndSession();

        [OperationContract]
        List<string> GetWarnings();
    }
}
