namespace Aon.ConsoleApp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //// Load the HTML document
            var html = System.IO.File.ReadAllText("01hh.htm");
            var bookLoader = new BookLoader();
            var book = bookLoader.LoadFromHtml(html);

            //// Example usage: Print the book title and front matters
            //Console.WriteLine($"Book Title: {book.Title}");
            ////foreach (var frontMatter in book.FrontMatters)
            ////{
            ////    PrintFrontMatter(frontMatter, 0);
            ////}

            //// Example usage: Print sections and choices
            //foreach (var section in book.Sections)
            //{
            //    Console.WriteLine($"Section: {section.Name}");
            //    Console.WriteLine(section.Content);
            //    foreach (var choice in section.Choices)
            //    {
            //        Console.WriteLine($"Choice: {choice.Text} -> {choice.TargetSectionName}");
            //    }
            //}

            // Access and print the content of a specific section
            string sectionName = "sect275"; // Replace with the desired section name
            var specificSection = book.GetSectionByName(sectionName);
            if (specificSection != null)
            {
                Console.WriteLine($"Content of section {sectionName}:");
                Console.WriteLine(specificSection.Content);
            }
            else
            {
                Console.WriteLine($"Section {sectionName} not found.");
            }

            //for (int i = 0; i < 100; i++)
            //{
            //    Console.WriteLine(Aon.Utilities.RandomNumberTable.GenerateRandomNumber());
            //}
        }

        static void PrintFrontMatter(FrontMatter frontMatter, int indentLevel)
        {
            var indent = new string(' ', indentLevel * 2);
            Console.WriteLine($"{indent}Title: {frontMatter.Title}");
            Console.WriteLine($"{indent}Content: {frontMatter.Content}");
            foreach (var subsection in frontMatter.Subsections)
            {
                PrintFrontMatter(subsection, indentLevel + 1);
            }
        }
    }
}
