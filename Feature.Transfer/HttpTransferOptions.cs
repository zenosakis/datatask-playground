using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Feature.Transfer
{
    public class HttpTransferOptions
    {
        public string BaseAddress { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 30;
    }
}
