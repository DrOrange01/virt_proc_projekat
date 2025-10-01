using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.EventArguments
{
    public class WarningRaisedEventArgs : EventArgs
    {
        public string WarningType { get; set; }
        public string Message { get; set; }
        public PvSample RelatedSample { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
