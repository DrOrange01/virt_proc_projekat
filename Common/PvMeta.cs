using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;

namespace Common
{
    [DataContract]
    public class PvMeta : IDisposable
    {
        [DataMember(IsRequired = true)]
        public string PlantId { get; set; } = "PLANT-001";

        [DataMember(IsRequired = true)]
        public string FileName { get; set; } = "";

        [DataMember(IsRequired = true)]
        public int TotalRows { get; set; }

        [DataMember(IsRequired = true)]
        public string SchemaVersion { get; set; } = "1.0";

        [DataMember(IsRequired = true)]
        public int RowLimitN { get; set; } = 100;

        [DataMember]
        public DateTime? SessionDateUtc { get; set; } = DateTime.UtcNow;
        private bool disposed = false;

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

        ~PvMeta()
        {
            Dispose(false);
        }
    }
}
