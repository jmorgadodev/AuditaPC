using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;

namespace AuditaPC.App.ViewModels;

public sealed class InstalledProgramsViewModel : ObservableObject
{
    private string _searchText = string.Empty;

    public InstalledProgramsViewModel(IEnumerable<ProgramRow> programs)
    {
        Programs = new ObservableCollection<ProgramRow>(programs);
        ProgramsView = CollectionViewSource.GetDefaultView(Programs);
        ProgramsView.Filter = FilterProgram;
        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);
    }

    public ObservableCollection<ProgramRow> Programs { get; }
    public ICollectionView ProgramsView { get; }
    public RelayCommand ClearSearchCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (Set(ref _searchText, value))
            {
                ProgramsView.Refresh();
                RaisePropertyChanged(nameof(ResultCountText));
                RaisePropertyChanged(nameof(IsEmpty));
            }
        }
    }

    public string ResultCountText
    {
        get
        {
            var visible = ProgramsView.Cast<object>().Count();
            return visible == Programs.Count
                ? $"{Programs.Count} programas"
                : $"{visible} de {Programs.Count} programas";
        }
    }

    public bool IsEmpty => !ProgramsView.Cast<object>().Any();

    private bool FilterProgram(object item)
    {
        if (item is not ProgramRow program)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        var query = SearchText.Trim();
        return Contains(program.Name, query)
               || Contains(program.Version, query)
               || Contains(program.Publisher, query)
               || Contains(program.Date, query)
               || Contains(program.Scope, query);
    }

    private static bool Contains(string? value, string query) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Contains(query, StringComparison.CurrentCultureIgnoreCase);
}
