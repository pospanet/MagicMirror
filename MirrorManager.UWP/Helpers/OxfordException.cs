using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MirrorManager.UWP.Helpers
{
    public class OxfordException : Exception
    {
        public OxfordException(string message) : base(message)
        {
        }
    }
}
