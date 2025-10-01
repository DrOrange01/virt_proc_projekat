using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.EventArguments
{
    public class TransferStartedEventArgs : EventArgs
    {
        public string SessionId { get; set; }
        public PvMeta Meta { get; set; }
    }
}
