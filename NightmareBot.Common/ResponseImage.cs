using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NightmareBot.Common
{
    public class ResponseImage
    {        
        public string? bucket { get; set; }
        public string? path { get; set; }
        public string? url { get; set; }
        public bool selected = false;
        public IEnumerable<RequestState>? enhance_requests { get; set; }
        public IEnumerable<RequestState>? dream_requests { get; set; }
    }
}
