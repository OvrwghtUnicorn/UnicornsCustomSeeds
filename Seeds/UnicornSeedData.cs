using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnicornsCustomSeeds.Seeds
{
    [Serializable]
    public class UnicornSeedData
    {
        public string seedId { get; set; }
        public string weedId { get; set; }
        public string baseSeedId { get; set; }
        public float price { get; set; }
    }
}
