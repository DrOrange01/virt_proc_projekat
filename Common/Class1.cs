using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace Common
{
    [DataContract]
    public enum nesto
    {
        [EnumMember]prvi,
        [EnumMember]drugi
    }
    [DataContract]
    public class Class1
    {
        [DataMember]
    }
}
