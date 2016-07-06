using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MirrorManager.UWP.Helpers
{

    public class OxfordError
    {
        public Error error { get; set; }

        public string Message => error.message;
    }

    public class Error
    {
        public string code { get; set; }
        public int statusCode { get; set; }
        public string message { get; set; }
    }

}
