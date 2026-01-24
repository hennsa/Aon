namespace Aon.Tools.BookImporter;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: Aon.Tools.BookImporter <input-dir> <output-dir>");
            return 1;
        }

        var inputDirectory = args[0];
        var outputDirectory = args[1];

        var importer = new BookImportService();
        BookImportResult result;
        try
        {
            result = await importer.ImportAsync(inputDirectory, outputDirectory);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        foreach (var warning in result.GeneralWarnings)
        {
            Console.Error.WriteLine(warning);
        }

        foreach (var issue in result.ValidationErrors)
        {
            Console.Error.WriteLine($"Validation errors in {issue.FilePath}:");
            Console.Error.WriteLine($"  - {issue.Message}");
        }

        foreach (var issue in result.MetadataWarnings)
        {
            Console.Error.WriteLine($"Rule metadata warnings in {issue.FilePath}:");
            Console.Error.WriteLine($"  - {issue.Message}");
        }

        Console.WriteLine($"Imported {result.ImportedCount} book file(s).");
        if (result.ValidationErrors.Count > 0)
        {
            Console.Error.WriteLine("Import completed with validation errors.");
            return 1;
        }

        return 0;
    }
}
