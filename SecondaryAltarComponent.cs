using ExileCore.PoEMemory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClickIt
{
    public class SecondaryAltarComponent
    {
        public SecondaryAltarComponent(Element element, string FirstUpside, string SecondUpside, string FirstDownside, string SecondDownside)
        {
            this.Element = element;
            this.FirstUpside = FirstUpside;
            this.SecondUpside = SecondUpside;
            this.FirstDownside = FirstDownside;
            this.SecondDownside = SecondDownside;
        }
        public Element Element;
        public string FirstUpside;
        public string SecondUpside;
        public string FirstDownside;
        public string SecondDownside;
    }
}
