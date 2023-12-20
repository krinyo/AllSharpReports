using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AllSharpReports
{
    // SavedQuery class
    [Serializable]
    public class SavedQuery
    {
        public string Name { get; set; }
        public string Query { get; set; }
    }
}
