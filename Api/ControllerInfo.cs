using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentaConAI.Api
{
    public class ControllerInfo
    {
        public string ControllerName { get; set; }
        public string FilePath { get; set; }
        public List<EndpointInfo> Endpoints { get; set; }

        public ControllerInfo()
        {
            Endpoints = new List<EndpointInfo>();
        }
    }
}
