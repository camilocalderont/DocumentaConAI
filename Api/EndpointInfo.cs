using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentaConAI.Api
{
    public class EndpointInfo
    {
        public string HttpMethod { get; set; }
        public string Route { get; set; }
        public string RequestObject { get; set; }
        public string ResponseObject { get; set; }
        public List<MethodParameter> Parameters { get; set; }

        public EndpointInfo()
        {
            Parameters = new List<MethodParameter>();
        }
    }
}
