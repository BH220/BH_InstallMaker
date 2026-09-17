using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BH_Install.Models
{
    public class ResLicense
    {
        public int result { get; set; }
        public string msg { get; set; }
        public bool unauthorized { get; set; }
        public DateTime? end_at { get; set; }
        public int max_count { get; set; }
        public int now_count { get; set; }
    }
}
