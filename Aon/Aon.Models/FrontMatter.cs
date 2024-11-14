namespace Aon.Models
{
    public class FrontMatter
    {
        public string Title { get; set; }
        public string Content { get; set; }
        public List<FrontMatter> Subsections { get; set; } = new List<FrontMatter>();
    }
}
