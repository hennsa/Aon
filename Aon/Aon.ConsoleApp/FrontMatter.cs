using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aon.ConsoleApp
{
    public class FrontMatter
    {
        public string Title { get; set; }
        public string Content { get; set; }
        public List<FrontMatter> Subsections { get; set; } = new List<FrontMatter>();
    }
}
