using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MirrorManager.UWP.Helpers
{

    public class OxfordIdentifyResponse
    {
        public string faceId { get; set; }
        public Candidate[] candidates { get; set; }
    }

    public class Candidate
    {
        public string personId { get; set; }
        public float confidence { get; set; }
    }

}
