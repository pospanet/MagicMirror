using System;

namespace Pospa.NET.MagicMirror.UI
{
    public class DrivingInfo
    {
        public DrivingInfo(decimal distance, decimal duration, decimal durationTrafic)
        {
            Distance = distance;
            Duration = duration;
            DurationTrafic = durationTrafic;
        }

        public decimal Distance { get; set; }
        public decimal Duration { get; set; }
        public decimal DurationTrafic { get; set; }
    }
}