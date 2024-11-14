namespace Aon.Models
{
    public class Section
    {
        public string Name { get; set; }
        public string Content { get; set; }
        public List<Choice> Choices { get; set; } = new List<Choice>();
    }
}
