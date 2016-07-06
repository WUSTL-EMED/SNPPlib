using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SNPPlib
{
    public class PagerCollection : Collection<string>
    {
        public PagerCollection()
        {
            Pagers = new List<string>();
        }

        internal IList<string> Pagers { get; set; }

        public new void Add(string pager)
        {
            if (!SnppClientProtocol.PagerIdFormat.IsMatch(pager))
                throw new ArgumentException(Resource.PagerIdNumeric, "pager");
            Pagers.Add(pager);
        }

        public void AddRange(IEnumerable<string> pagers)
        {
            foreach (var pager in pagers)
            {
                if (!SnppClientProtocol.PagerIdFormat.IsMatch(pager))
                    throw new ArgumentException(Resource.PagerIdNumeric, "pager");
                Pagers.Add(pager);
            }
        }

        public override string ToString()
        {
            return String.Join(", ", Pagers);
        }

        protected override void InsertItem(int index, string pager)
        {
            if (!SnppClientProtocol.PagerIdFormat.IsMatch(pager))
                throw new ArgumentException(Resource.PagerIdNumeric, "pager");
            Pagers.Insert(index, pager);
        }

        protected override void SetItem(int index, string pager)
        {
            if (!SnppClientProtocol.PagerIdFormat.IsMatch(pager))
                throw new ArgumentException(Resource.PagerIdNumeric, "pager");
            Pagers[index] = pager;
        }
    }
}