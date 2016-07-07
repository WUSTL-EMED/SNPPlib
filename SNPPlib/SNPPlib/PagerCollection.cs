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

        public new void Add(string item)
        {
            if (!SnppClientProtocol.PagerIdFormat.IsMatch(item))
                throw new ArgumentException(Resource.PagerIdNumeric, "item");
            Pagers.Add(item);
        }

        /// <exception cref="System.ArgumentNullException">The <paramref name="items"/> parameter was null.</exception>
        public void AddRange(IEnumerable<string> items)
        {
            if (items == null)
                throw new ArgumentNullException("items");

            foreach (var item in items)
            {
                if (!SnppClientProtocol.PagerIdFormat.IsMatch(item))
                    throw new ArgumentException(Resource.PagerIdNumeric, "items");
                Pagers.Add(item);
            }
        }

        public override string ToString()
        {
            return String.Join(", ", Pagers);
        }

        protected override void InsertItem(int index, string item)
        {
            if (!SnppClientProtocol.PagerIdFormat.IsMatch(item))
                throw new ArgumentException(Resource.PagerIdNumeric, "item");
            Pagers.Insert(index, item);
        }

        protected override void SetItem(int index, string item)
        {
            if (!SnppClientProtocol.PagerIdFormat.IsMatch(item))
                throw new ArgumentException(Resource.PagerIdNumeric, "item");
            Pagers[index] = item;
        }
    }
}