using System;

namespace Pospa.Mirror.Common.DTO
{

    public class CalendarEvent
    {
        public Value[] value { get; set; }
    }

    public class Value
    {
        public string odataetag { get; set; }
        public string id { get; set; }
        public DateTime createdDateTime { get; set; }
        public DateTime lastModifiedDateTime { get; set; }
        public string changeKey { get; set; }
        public object[] categories { get; set; }
        public string originalStartTimeZone { get; set; }
        public string originalEndTimeZone { get; set; }
        public Responsestatus responseStatus { get; set; }
        public string iCalUId { get; set; }
        public int reminderMinutesBeforeStart { get; set; }
        public bool isReminderOn { get; set; }
        public bool hasAttachments { get; set; }
        public string subject { get; set; }
        public Body body { get; set; }
        public string bodyPreview { get; set; }
        public string importance { get; set; }
        public string sensitivity { get; set; }
        public Start start { get; set; }
        public End end { get; set; }
        public Location location { get; set; }
        public bool isAllDay { get; set; }
        public bool isCancelled { get; set; }
        public bool isOrganizer { get; set; }
        public object recurrence { get; set; }
        public bool responseRequested { get; set; }
        public object seriesMasterId { get; set; }
        public string showAs { get; set; }
        public string type { get; set; }
        public object[] attendees { get; set; }
        public Organizer organizer { get; set; }
        public string webLink { get; set; }
    }

    public class Responsestatus
    {
        public string response { get; set; }
        public DateTime time { get; set; }
    }

    public class Body
    {
        public string contentType { get; set; }
        public string content { get; set; }
    }

    public class Start
    {
        public DateTime dateTime { get; set; }
        public string timeZone { get; set; }
    }

    public class End
    {
        public DateTime dateTime { get; set; }
        public string timeZone { get; set; }
    }

    public class Location
    {
        public string displayName { get; set; }
        public Address address { get; set; }
    }

    public class Address
    {
    }

    public class Organizer
    {
        public Emailaddress emailAddress { get; set; }
    }

    public class Emailaddress
    {
        public string name { get; set; }
        public string address { get; set; }
    }

}
