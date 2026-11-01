using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PetroRes_Assignment
{
    public class WellInfo
    {
        public double StartDepth { get; set; }
        public double StopDepth { get; set; }
        public double? Step { get; set; }
        public double? NullValue { get; set; }

        public string Company { get; set; }
        public string WellName { get; set; }
        public string Field { get; set; }
        public string Location { get; set; }
        public string County { get; set; }
        public string State { get; set; }
        public string Country { get; set; }
        public string ServiceCompany { get; set; }

        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public string ApiNumber { get; set; }
        public string LogDate { get; set; }
        public string Uwi { get; set; }
    }
}
