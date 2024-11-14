namespace Aon.Models
{
    public class Book
    {
        public string Title { get; set; }
        public List<FrontMatter> FrontMatters { get; set; } = new List<FrontMatter>();
        public List<Section> Sections { get; set; } = new List<Section>();

        public Section GetSectionByName(string name)
        {
            return Sections.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
