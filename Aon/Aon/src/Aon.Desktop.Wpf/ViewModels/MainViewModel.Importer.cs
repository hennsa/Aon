using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Aon.Tools.BookImporter;
using Forms = System.Windows.Forms;

namespace Aon.Desktop.Wpf.ViewModels;

public sealed partial class MainViewModel
{
    private readonly BookImportService _bookImportService = new();
    private readonly RelayCommand _browseImportInputCommand;
    private readonly RelayCommand _browseImportOutputCommand;
    private readonly RelayCommand _runImportCommand;
    private string _importInputDirectory = string.Empty;
    private string _importOutputDirectory = string.Empty;
    private string _importStatus = "Select input and output folders to begin.";
    private int _importProgressValue;
    private int _importProgressMaximum = 1;
    private bool _isImporting;

    public ObservableCollection<string> ImportWarnings { get; } = new();
    public ObservableCollection<string> ImportErrors { get; } = new();

    public string ImportInputDirectory
    {
        get => _importInputDirectory;
        set
        {
            if (_importInputDirectory == value)
            {
                return;
            }

            _importInputDirectory = value;
            OnPropertyChanged();
            _runImportCommand.RaiseCanExecuteChanged();
        }
    }

    public string ImportOutputDirectory
    {
        get => _importOutputDirectory;
        set
        {
            if (_importOutputDirectory == value)
            {
                return;
            }

            _importOutputDirectory = value;
            OnPropertyChanged();
            _runImportCommand.RaiseCanExecuteChanged();
        }
    }

    public string ImportStatus
    {
        get => _importStatus;
        set
        {
            if (_importStatus == value)
            {
                return;
            }

            _importStatus = value;
            OnPropertyChanged();
        }
    }

    public int ImportProgressValue
    {
        get => _importProgressValue;
        set
        {
            if (_importProgressValue == value)
            {
                return;
            }

            _importProgressValue = value;
            OnPropertyChanged();
        }
    }

    public int ImportProgressMaximum
    {
        get => _importProgressMaximum;
        set
        {
            if (_importProgressMaximum == value)
            {
                return;
            }

            _importProgressMaximum = value;
            OnPropertyChanged();
        }
    }

    public bool IsImporting
    {
        get => _isImporting;
        set
        {
            if (_isImporting == value)
            {
                return;
            }

            _isImporting = value;
            OnPropertyChanged();
            _runImportCommand.RaiseCanExecuteChanged();
            _browseImportInputCommand.RaiseCanExecuteChanged();
            _browseImportOutputCommand.RaiseCanExecuteChanged();
        }
    }

    public bool HasImportWarnings => ImportWarnings.Count > 0;
    public bool HasImportErrors => ImportErrors.Count > 0;

    public RelayCommand BrowseImportInputCommand => _browseImportInputCommand;
    public RelayCommand BrowseImportOutputCommand => _browseImportOutputCommand;
    public RelayCommand RunImportCommand => _runImportCommand;

    private bool CanRunImport()
    {
        return !IsImporting
            && !string.IsNullOrWhiteSpace(ImportInputDirectory)
            && Directory.Exists(ImportInputDirectory)
            && !string.IsNullOrWhiteSpace(ImportOutputDirectory);
    }

    private void SelectImportDirectory(bool isInput)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = isInput ? "Select the input folder containing HTML book files." : "Select the output folder for JSON exports.",
            SelectedPath = isInput ? ImportInputDirectory : ImportOutputDirectory,
            ShowNewFolderButton = true
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK)
        {
            return;
        }

        if (isInput)
        {
            ImportInputDirectory = dialog.SelectedPath;
        }
        else
        {
            ImportOutputDirectory = dialog.SelectedPath;
        }
    }

    private async Task RunImportAsync()
    {
        if (string.IsNullOrWhiteSpace(ImportInputDirectory) || !Directory.Exists(ImportInputDirectory))
        {
            System.Windows.MessageBox.Show("Select a valid input directory before running the import.", "Book Importer", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(ImportOutputDirectory))
        {
            System.Windows.MessageBox.Show("Select an output directory before running the import.", "Book Importer", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsImporting = true;
        ImportWarnings.Clear();
        ImportErrors.Clear();
        OnPropertyChanged(nameof(HasImportWarnings));
        OnPropertyChanged(nameof(HasImportErrors));
        ImportProgressValue = 0;
        ImportProgressMaximum = 1;
        ImportStatus = "Starting import...";

        try
        {
            var progress = new Progress<BookImportProgress>(UpdateImportProgress);
            var result = await _bookImportService.ImportAsync(ImportInputDirectory, ImportOutputDirectory, progress);

            foreach (var warning in result.GeneralWarnings)
            {
                ImportWarnings.Add(warning);
            }

            foreach (var warning in result.MetadataWarnings)
            {
                ImportWarnings.Add($"{Path.GetFileName(warning.FilePath)}: {warning.Message}");
            }

            foreach (var error in result.ValidationErrors)
            {
                ImportErrors.Add($"{Path.GetFileName(error.FilePath)}: {error.Message}");
            }

            OnPropertyChanged(nameof(HasImportWarnings));
            OnPropertyChanged(nameof(HasImportErrors));

            ImportStatus = result.ValidationErrors.Count > 0
                ? $"Imported {result.ImportedCount} book file(s) with validation errors."
                : $"Imported {result.ImportedCount} book file(s).";
        }
        catch (Exception ex)
        {
            ImportErrors.Add(ex.Message);
            OnPropertyChanged(nameof(HasImportErrors));
            ImportStatus = "Import failed.";
        }
        finally
        {
            IsImporting = false;
        }
    }

    private void UpdateImportProgress(BookImportProgress progress)
    {
        var totalCount = Math.Max(progress.TotalCount, 1);
        ImportProgressMaximum = totalCount;
        ImportProgressValue = Math.Min(progress.ProcessedCount, totalCount);
        var fileName = string.IsNullOrWhiteSpace(progress.CurrentFile)
            ? ""
            : $" ({Path.GetFileName(progress.CurrentFile)})";
        ImportStatus = $"Importing {progress.ProcessedCount} of {totalCount}{fileName}";
    }
}
