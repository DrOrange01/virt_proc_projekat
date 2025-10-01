using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;

namespace Common
{
    [DataContract]
    public class PvSample
    {
        [DataMember(IsRequired = true)]
        public int RowIndex { get; set; }

        [DataMember]
        public string Day { get; set; }

        [DataMember]
        public string Hour { get; set; }

        [DataMember]
        public double? AcPwrt { get; set; }

        [DataMember]
        public double? DcVolt { get; set; }

        [DataMember]
        public double? Temper { get; set; }

        [DataMember]
        public double? Vl1to2 { get; set; }

        [DataMember]
        public double? Vl2to3 { get; set; }

        [DataMember]
        public double? Vl3to1 { get; set; }

        [DataMember]
        public double? AcCur1 { get; set; }

        [DataMember]
        public double? AcVlt1 { get; set; }
    }
}
