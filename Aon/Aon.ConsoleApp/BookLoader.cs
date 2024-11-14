using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aon.ConsoleApp
{
    public class BookLoader
    {
        public Book LoadFromHtml(string html)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var book = new Book
            {
                Title = doc.DocumentNode.SelectSingleNode("//h1/a[@name='title']").InnerText.Trim()
            };

            // Load FrontMatters
            var frontMatterNodes = doc.DocumentNode.SelectNodes("//div[@class='frontmatter']");
            if (frontMatterNodes != null)
            {
                foreach (var frontMatterNode in frontMatterNodes)
                {
                    var frontMatter = LoadFrontMatter(frontMatterNode);
                    book.FrontMatters.Add(frontMatter);
                }
            }

            // Load Sections
            var sectionNodes = doc.DocumentNode.SelectNodes("//h3/a[starts-with(@name, 'sect')]");
            if (sectionNodes != null)
            {
                foreach (var sectionNode in sectionNodes)
                {
                    var section = LoadSection(sectionNode);
                    book.Sections.Add(section);
                }
            }

            return book;
        }

        private FrontMatter LoadFrontMatter(HtmlNode frontMatterNode)
        {
            var titleNode = frontMatterNode.SelectSingleNode(".//h2/a");
            var frontMatter = new FrontMatter
            {
                Title = titleNode?.InnerText.Trim()
            };

            var contentNodes = titleNode?.ParentNode?.NextSibling;
            var content = new List<string>();

            while (contentNodes != null && contentNodes.Name != "h2")
            {
                if (contentNodes.Name == "h3")
                {
                    var subsection = LoadFrontMatter(contentNodes);
                    frontMatter.Subsections.Add(subsection);
                }
                else
                {
                    content.Add(contentNodes.InnerText.Trim());
                }

                contentNodes = contentNodes.NextSibling;
            }

            frontMatter.Content = string.Join("\n", content);
            return frontMatter;
        }

        private Section LoadSection(HtmlNode sectionNode)
        {
            var sectionName = sectionNode.GetAttributeValue("name", string.Empty);
            var sectionContentNode = sectionNode.ParentNode.NextSibling;
            var sectionContent = new List<string>();
            var choices = new List<Choice>();

            while (sectionContentNode != null && sectionContentNode.Name != "h3")
            {
                if (sectionContentNode.Name == "p" && sectionContentNode.HasClass("choice"))
                {
                    var choiceNode = sectionContentNode.SelectSingleNode(".//a[@href]");
                    if (choiceNode != null)
                    {
                        var choice = new Choice
                        {
                            Text = choiceNode.InnerText.Trim(),
                            TargetSectionName = choiceNode.GetAttributeValue("href", string.Empty).TrimStart('#')
                        };
                        choices.Add(choice);
                    }
                }
                else
                {
                    sectionContent.Add(sectionContentNode.InnerText.Trim());
                }

                sectionContentNode = sectionContentNode.NextSibling;
            }

            return new Section
            {
                Name = sectionName,
                Content = string.Join("\n", sectionContent),
                Choices = choices
            };
        }
    }
}
