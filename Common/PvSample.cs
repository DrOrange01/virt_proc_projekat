using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.IO;

namespace Common
{
    [DataContract]
    public class PvSample : IDisposable
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
        private bool disposed = false;

        public bool IsValid(out string errorMessage)
        {
            errorMessage = string.Empty;

            if (AcPwrt.HasValue && AcPwrt < 0)
            {
                errorMessage = "AC Power cannot be negative";
                return false;
            }

            if (DcVolt.HasValue && DcVolt <= 0)
            {
                errorMessage = "DC Voltage must be positive";
                return false;
            }

            return true;
        }

        // Provera da li je vrednost sentinel (32767.0)
        public static double? ProcessSentinel(double value)
        {
            if (Math.Abs(value - 32767.0) < 0.001)
            {
                return null;
            }
            return value;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // Oslobađanje managed resursa
                }
                disposed = true;
            }
        }

        ~PvSample()
        {
            Dispose(false);
        }
    }
}
