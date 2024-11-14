using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aon.ConsoleApp
{
    public class Section
    {
        public string Name { get; set; }
        public string Content { get; set; }
        public List<Choice> Choices { get; set; } = new List<Choice>();
    }
}
