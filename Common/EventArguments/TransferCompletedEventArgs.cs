using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.EventArguments
{
    public class TransferCompletedEventArgs : EventArgs
    {
        public string SessionId { get; set; }
        public int TotalSamples { get; set; }
        public TimeSpan Duration { get; set; }
    }
}
