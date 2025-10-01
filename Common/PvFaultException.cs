using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    [DataContract]
    public class PvFaultException
    {
        [DataMember]
        public string Message { get; set; }

        [DataMember]
        public string ErrorCode { get; set; }

        public PvFaultException() { }

        public PvFaultException(string message, string errorCode = "GENERAL_ERROR")
        {
            Message = message;
            ErrorCode = errorCode;
        }
    }
}
