using ComicSort.UI.Models.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ComicSort.UI.ViewModels.Dialogs;

public sealed partial class SmartListEditorDialogViewModel : ViewModelBase
{
    private readonly ObservableCollection<SmartListRuleEditorRowViewModel> _rules = [];

    public SmartListEditorDialogViewModel(SmartListEditorResult initialState)
    {
        Rules = new ReadOnlyObservableCollection<SmartListRuleEditorRowViewModel>(_rules);
        LoadInitialState(initialState);
    }

    [ObservableProperty]
    private string name = "New Smart List";

    [ObservableProperty]
    private string notes = string.Empty;

    [ObservableProperty]
    private bool showAdvancedOptions;

    [ObservableProperty]
    private bool showInQuickOpen;

    [ObservableProperty]
    private bool limitToBooksEnabled;

    [ObservableProperty]
    private int limitBookCount = 25;

    [ObservableProperty]
    private string selectedGlobalMatchMode = "All";

    [ObservableProperty]
    private string selectedScope = "Library";

    [ObservableProperty]
    private string statusText = "Configure match rules and click OK to save.";

    public IReadOnlyList<string> MatchModes { get; } = ["All", "Any"];

    public IReadOnlyList<string> ScopeOptions { get; } = ["Library"];

    public IReadOnlyList<string> OperatorOptions { get; } =
    [
        "is",
        "contains",
        "contains any of",
        "contains all of",
        "starts with",
        "ends with",
        "list contains",
        "regular expression"
    ];

    public IReadOnlyList<SmartListFieldCategory> FieldCategories { get; } = BuildFieldCategories();

    public ReadOnlyObservableCollection<SmartListRuleEditorRowViewModel> Rules { get; }

    public event EventHandler<SmartListEditorDialogCloseRequestedEventArgs>? CloseRequested;

    [RelayCommand]
    private void ToggleRuleNegation(SmartListRuleEditorRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        row.IsNegated = !row.IsNegated;
    }

    [RelayCommand]
    private void ToggleShowAdvancedOptions()
    {
        ShowAdvancedOptions = !ShowAdvancedOptions;
    }

    [RelayCommand]
    private void SelectField(SmartListFieldSelection? selection)
    {
        if (selection?.Row is null || string.IsNullOrWhiteSpace(selection.FieldName))
        {
            return;
        }

        selection.Row.SelectedField = selection.FieldName.Trim();
    }

    [RelayCommand]
    private void AddRuleAfter(SmartListRuleEditorRowViewModel? row)
    {
        InsertAfter(row, CreateRuleRow());
        StatusText = "Rule added.";
    }

    [RelayCommand]
    private void AddGroupAfter(SmartListRuleEditorRowViewModel? row)
    {
        var groupHeader = CreateGroupHeaderRow();
        var firstRule = CreateRuleRow();

        var index = ResolveInsertIndex(row);
        _rules.Insert(index, groupHeader);
        _rules.Insert(index + 1, firstRule);
        StatusText = "Group added.";
    }

    [RelayCommand]
    private void DeleteRule(SmartListRuleEditorRowViewModel? row)
    {
        if (row is null || !_rules.Contains(row))
        {
            return;
        }

        if (_rules.Count == 1)
        {
            row.Value = string.Empty;
            row.IsNegated = false;
            row.SelectedField = "All";
            row.SelectedOperator = "contains";
            StatusText = "At least one rule is required.";
            return;
        }

        _rules.Remove(row);
        StatusText = "Rule deleted.";
    }

    [RelayCommand]
    private void MoveRuleUp(SmartListRuleEditorRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        var index = _rules.IndexOf(row);
        if (index <= 0)
        {
            return;
        }

        _rules.Move(index, index - 1);
    }

    [RelayCommand]
    private void MoveRuleDown(SmartListRuleEditorRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        var index = _rules.IndexOf(row);
        if (index < 0 || index >= _rules.Count - 1)
        {
            return;
        }

        _rules.Move(index, index + 1);
    }

    [RelayCommand]
    private void Previous()
    {
        StatusText = "Previous page is not available in this phase.";
    }

    [RelayCommand]
    private void Next()
    {
        StatusText = "Next page is not available in this phase.";
    }

    [RelayCommand]
    private void Query()
    {
        StatusText = $"Query preview: {_rules.Count} rule(s).";
    }

    [RelayCommand]
    private void Apply()
    {
        StatusText = "Changes applied to editor state.";
    }

    [RelayCommand]
    private void Ok()
    {
        var normalizedName = string.IsNullOrWhiteSpace(Name) ? "New Smart List" : Name.Trim();
        Name = normalizedName;

        var result = new SmartListEditorResult
        {
            Name = normalizedName,
            Notes = Notes,
            MatchMode = SelectedGlobalMatchMode,
            Scope = SelectedScope,
            ShowInQuickOpen = ShowInQuickOpen,
            LimitToBooksEnabled = LimitToBooksEnabled,
            LimitBookCount = Math.Max(1, LimitBookCount),
            Rules = _rules.Select(ToRuleResult).ToArray()
        };

        CloseRequested?.Invoke(this, new SmartListEditorDialogCloseRequestedEventArgs(result));
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(this, new SmartListEditorDialogCloseRequestedEventArgs(null));
    }

    private static SmartListRuleResult ToRuleResult(SmartListRuleEditorRowViewModel row)
    {
        return new SmartListRuleResult
        {
            IsGroupHeader = row.IsGroupHeader,
            IsNegated = row.IsNegated,
            MatchMode = row.SelectedMatchMode,
            Field = row.SelectedField,
            Operator = row.SelectedOperator,
            Value = row.Value
        };
    }

    private static SmartListRuleEditorRowViewModel CreateRuleRow()
    {
        return new SmartListRuleEditorRowViewModel
        {
            SelectedMatchMode = "All",
            SelectedField = "All",
            SelectedOperator = "contains",
            Value = string.Empty
        };
    }

    private static SmartListRuleEditorRowViewModel ToRuleRow(SmartListRuleResult rule)
    {
        if (rule.IsGroupHeader)
        {
            return new SmartListRuleEditorRowViewModel
            {
                IsGroupHeader = true,
                SelectedMatchMode = string.IsNullOrWhiteSpace(rule.MatchMode) ? "All" : rule.MatchMode.Trim(),
                SelectedField = "of the following rules:",
                SelectedOperator = string.Empty,
                Value = string.Empty
            };
        }

        return new SmartListRuleEditorRowViewModel
        {
            IsGroupHeader = false,
            IsNegated = rule.IsNegated,
            SelectedMatchMode = string.IsNullOrWhiteSpace(rule.MatchMode) ? "All" : rule.MatchMode.Trim(),
            SelectedField = string.IsNullOrWhiteSpace(rule.Field) ? "All" : rule.Field.Trim(),
            SelectedOperator = string.IsNullOrWhiteSpace(rule.Operator) ? "contains" : rule.Operator.Trim(),
            Value = rule.Value ?? string.Empty
        };
    }

    private static SmartListRuleEditorRowViewModel CreateGroupHeaderRow()
    {
        return new SmartListRuleEditorRowViewModel
        {
            IsGroupHeader = true,
            SelectedMatchMode = "All",
            SelectedField = "of the following rules:",
            SelectedOperator = string.Empty,
            Value = string.Empty
        };
    }

    private void InsertAfter(SmartListRuleEditorRowViewModel? row, SmartListRuleEditorRowViewModel newRow)
    {
        var insertIndex = ResolveInsertIndex(row);
        _rules.Insert(insertIndex, newRow);
    }

    private int ResolveInsertIndex(SmartListRuleEditorRowViewModel? row)
    {
        if (row is null)
        {
            return _rules.Count;
        }

        var index = _rules.IndexOf(row);
        return index < 0 ? _rules.Count : index + 1;
    }

    private void LoadInitialState(SmartListEditorResult? state)
    {
        var initial = state ?? new SmartListEditorResult();

        Name = string.IsNullOrWhiteSpace(initial.Name) ? "New Smart List" : initial.Name.Trim();
        Notes = initial.Notes ?? string.Empty;
        SelectedGlobalMatchMode = string.IsNullOrWhiteSpace(initial.MatchMode) ? "All" : initial.MatchMode.Trim();
        SelectedScope = string.IsNullOrWhiteSpace(initial.Scope) ? "Library" : initial.Scope.Trim();
        ShowInQuickOpen = initial.ShowInQuickOpen;
        LimitToBooksEnabled = initial.LimitToBooksEnabled;
        LimitBookCount = Math.Max(1, initial.LimitBookCount);
        ShowAdvancedOptions = !string.IsNullOrWhiteSpace(Notes) || ShowInQuickOpen || LimitToBooksEnabled;

        _rules.Clear();
        if (initial.Rules is { Count: > 0 })
        {
            foreach (var rule in initial.Rules)
            {
                _rules.Add(ToRuleRow(rule));
            }
        }

        if (_rules.Count == 0)
        {
            _rules.Add(CreateRuleRow());
        }
    }

    private static IReadOnlyList<SmartListFieldCategory> BuildFieldCategories()
    {
        return
        [
            new SmartListFieldCategory("All",
            [
                "Added",
                "Age Rating",
                "All",
                "Alternate Count",
                "Alternate Number",
                "Alternate Series",
                "Black and White",
                "Book Age",
                "Book Collection Status",
                "Book Condition",
                "Book Location",
                "Book Notes",
                "Book Owner",
                "Book Price",
                "Book Store",
                "Bookmark Count"
            ]),
            new SmartListFieldCategory("A-B",
            [
                "Added",
                "Age Rating",
                "All",
                "Alternate Count",
                "Alternate Number",
                "Alternate Series",
                "Black and White",
                "Book Age",
                "Book Collection Status",
                "Book Condition",
                "Book Location",
                "Book Notes",
                "Book Owner",
                "Book Price",
                "Book Store",
                "Bookmark Count"
            ]),
            new SmartListFieldCategory("C-H",
            [
                "Characters",
                "Colorist",
                "Community Rating",
                "Count",
                "Cover Artist",
                "Custom Value",
                "Day",
                "Editor",
                "Expression",
                "File",
                "File Created",
                "File Directory",
                "File Format",
                "File Modified",
                "File Path",
                "File Size",
                "Format",
                "Genre",
                "Has Custom Values"
            ]),
            new SmartListFieldCategory("I-O",
            [
                "Imprint",
                "Inker",
                "Is Checked",
                "Is Linked",
                "Is Missing",
                "ISBN",
                "Language",
                "Letterer",
                "Locations",
                "Main Character/Team",
                "Manga",
                "Modified Info",
                "Month",
                "My Rating",
                "New Pages",
                "Notes",
                "Number",
                "Only Duplicates",
                "Opened"
            ]),
            new SmartListFieldCategory("P-R",
            [
                "Page Count",
                "Penciller",
                "Published",
                "Publisher",
                "Read Percentage",
                "Released",
                "Review"
            ]),
            new SmartListFieldCategory("S",
            [
                "Scanning Information",
                "Series",
                "Series complete",
                "Series Group",
                "Series: All complete",
                "Series: Average Community Rating",
                "Series: Average Rating",
                "Series: Biggest Gap",
                "Series: Book added",
                "Series: Book Count",
                "Series: Book released",
                "Series: End of Gap",
                "Series: First Number",
                "Series: First Year",
                "Series: Gaps",
                "Series: Highest Count",
                "Series: Last Number",
                "Series: Last Year",
                "Series: Lowest Count",
                "Series: Opened",
                "Series: Pages",
                "Series: Pages Read",
                "Series: Percent Read",
                "Series: Published",
                "Series: Running Time Years",
                "Series: Start of Gap",
                "Story Arc",
                "Summary"
            ]),
            new SmartListFieldCategory("T-Y",
            [
                "Tags",
                "Teams",
                "Title",
                "Translator",
                "User Scripts",
                "Volume",
                "Web",
                "Week",
                "Writer",
                "Year"
            ])
        ];
    }
}

public sealed partial class SmartListRuleEditorRowViewModel : ObservableObject
{
    [ObservableProperty]
    private bool isGroupHeader;

    [ObservableProperty]
    private bool isNegated;

    [ObservableProperty]
    private string selectedMatchMode = "All";

    [ObservableProperty]
    private string selectedField = "All";

    [ObservableProperty]
    private string selectedOperator = "contains";

    [ObservableProperty]
    private string value = string.Empty;
}

public sealed class SmartListFieldCategory
{
    public SmartListFieldCategory(string header, IReadOnlyList<string> fields)
    {
        Header = header;
        Fields = fields;
    }

    public string Header { get; }

    public IReadOnlyList<string> Fields { get; }
}

public sealed class SmartListFieldSelection
{
    public SmartListFieldSelection(SmartListRuleEditorRowViewModel row, string fieldName)
    {
        Row = row;
        FieldName = fieldName;
    }

    public SmartListRuleEditorRowViewModel Row { get; }

    public string FieldName { get; }
}

public sealed class SmartListEditorDialogCloseRequestedEventArgs : EventArgs
{
    public SmartListEditorDialogCloseRequestedEventArgs(SmartListEditorResult? result)
    {
        Result = result;
    }

    public SmartListEditorResult? Result { get; }
}
