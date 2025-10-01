using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.EventArguments
{
    public class SampleReceivedEventArgs : EventArgs
    {
        public PvSample Sample { get; set; }
        public int TotalReceived { get; set; }
        public double ProgressPercentage { get; set; }
    }
}
