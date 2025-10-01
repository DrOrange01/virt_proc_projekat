using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    [ServiceContract]
    public interface IPvDataService
    {
        [OperationContract]
        [FaultContract(typeof(PvFaultException))]
        SessionResult StartSession(PvMeta meta);

        [OperationContract]
        [FaultContract(typeof(PvFaultException))]
        SampleResult PushSample(PvSample sample);

        [OperationContract]
        [FaultContract(typeof(PvFaultException))]
        SessionResult EndSession();
    }
}
