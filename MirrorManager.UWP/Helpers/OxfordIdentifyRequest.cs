using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MirrorManager.UWP.Helpers
{
    public class OxfordIdentifyRequest
    {
        [JsonProperty("personGroupId")]
        public string PersonGroupId { get; set; }

        [JsonProperty("faceIds")]
        public string[] FaceIds { get; set; }

        [JsonProperty("maxNumOfCandidatesReturned")]
        public int MaxNumOfCandidatesReturned { get; set; }

        public OxfordIdentifyRequest(string groupId, string faceId, int maxNumOfCandidates = 1)
        {
            this.PersonGroupId = groupId;
            this.FaceIds = new string[] { faceId };
            this.MaxNumOfCandidatesReturned = maxNumOfCandidates;
        }
    }
}
