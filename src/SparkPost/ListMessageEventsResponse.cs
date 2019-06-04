using System.Collections.Generic;

namespace SparkPost
{
    public class ListMessageEventsResponse : Response
    {
        public ListMessageEventsResponse()
        {
            MessageEvents = new MessageEvent[] {};
        }

        public IEnumerable<MessageEvent> MessageEvents { get; set; }

        public PageLink Links { get; set; }

        public int TotalCount { get; set; }
    }
}