using System;
using System.Collections.Generic;

namespace SNPPlib
{
    //TODO: Allow the specification of a protocl level and restrict capabilities to that level?
    //E.g. If level one, replace newlines with space since DATA is required for multi-line?
    public class SnppMessage
    {
        public string Message { get; set; }

        public PagerCollection Pagers { get; private set; }

        public ServiceLevel? ServiceLevel { get; set; }

        public string Subject { get; set; }

        internal IList<string> Data
        {
            //Multi-line messages need to be sent via DATA.
            get
            {
                //TODO: Keep empty? Pagers are limited so I'm not sure we should keep them.
                //Will this split in the specified order?
                return Message.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        #region Constructors

        public SnppMessage()
        {
            Pagers = new PagerCollection();
        }

        public SnppMessage(string pager)
        {
            Pagers = new PagerCollection();
            Pagers.Add(pager);
        }

        #endregion Constructors
    }
}