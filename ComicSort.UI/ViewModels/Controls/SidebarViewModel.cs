using ComicSort.Engine.Services;
using ComicSort.Engine.Settings;
using ComicSort.Engine.Models;
using ComicSort.UI.Models;
using ComicSort.UI.Models.Dialogs;
using ComicSort.UI.Services;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ComicSort.UI.ViewModels.Controls;

public partial class SidebarViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IDialogService _dialogService;
    private readonly IScanRepository _scanRepository;
    private readonly IScanService _scanService;
    private readonly IComicDatabaseService _comicDatabaseService;
    private readonly ISmartListExpressionService _smartListExpressionService;
    private readonly Dictionary<SidebarItem, ComicListItem> _settingsMap = [];
    private readonly SidebarItem _allComicsItem;
    private int _libraryCountBaseline;

    public SidebarViewModel(
        ISettingsService settingsService,
        IDialogService dialogService,
        IScanRepository scanRepository,
        IScanService scanService,
        IComicDatabaseService comicDatabaseService,
        ISmartListExpressionService smartListExpressionService)
    {
        _settingsService = settingsService;
        _dialogService = dialogService;
        _scanRepository = scanRepository;
        _scanService = scanService;
        _comicDatabaseService = comicDatabaseService;
        _smartListExpressionService = smartListExpressionService;

        _allComicsItem = new SidebarItem("All Comics", 0);

        LibraryItems =
        [
            _allComicsItem
        ];

        SmartListItems = BuildSmartListItems(settingsService.CurrentSettings);

        SelectedSidebarItem = LibraryItems.FirstOrDefault();
        SelectedSmartListItem = SmartListItems.FirstOrDefault();

        _scanService.ProgressChanged += OnScanProgressChanged;
        _scanService.StateChanged += OnScanStateChanged;
        _ = LoadLibraryCountAsync();
    }

    public ObservableCollection<SidebarItem> LibraryItems { get; }

    public ObservableCollection<SidebarItem> SmartListItems { get; }

    [ObservableProperty]
    private SidebarItem? selectedSidebarItem;

    [ObservableProperty]
    private SidebarItem? selectedSmartListItem;

    [ObservableProperty]
    private string libraryHeaderText = "My Library";

    public event EventHandler<ComicGridFilterRequest>? ActiveFilterChanged;

    partial void OnSelectedSidebarItemChanged(SidebarItem? value)
    {
        if (value is null)
        {
            return;
        }

        if (ReferenceEquals(value, _allComicsItem))
        {
            if (SelectedSmartListItem is not null)
            {
                SelectedSmartListItem = null;
            }

            RaiseAllComicsFilterChanged();
        }
    }

    partial void OnSelectedSmartListItemChanged(SidebarItem? value)
    {
        if (value is null || value.IsHeader)
        {
            return;
        }

        if (!_settingsMap.TryGetValue(value, out var model))
        {
            return;
        }

        if (!string.Equals(model.Type, "ComicSmartListItem", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (SelectedSidebarItem is not null)
        {
            SelectedSidebarItem = null;
        }

        RaiseSmartListFilterChanged(model);
    }

    [RelayCommand]
    private void OpenLibrarySettings()
    {
        // Placeholder action for the library settings button.
    }

    [RelayCommand]
    private async Task AddSmartList()
    {
        var target = SelectedSmartListItem;
        if (target is not null)
        {
            await AddSmartListUnderAsync(target);
            return;
        }

        var defaultParent = GetDefaultSmartListRoot();
        if (defaultParent is not null)
        {
            await AddSmartListUnderAsync(defaultParent);
        }
    }

    [RelayCommand]
    private async Task AddChildSmartList(SidebarItem? item)
    {
        if (item is null)
        {
            return;
        }

        await AddSmartListUnderAsync(item);
    }

    [RelayCommand]
    private void RenameSmartList(SidebarItem? item)
    {
        if (item is null || item.IsHeader)
        {
            return;
        }

        item.IsEditing = true;
        SelectedSmartListItem = item;
    }

    [RelayCommand]
    private async Task EditSmartList(SidebarItem? item)
    {
        if (item is null || item.IsHeader)
        {
            return;
        }

        if (!_settingsMap.TryGetValue(item, out var listModel))
        {
            return;
        }

        var editorResult = await _dialogService.ShowSmartListEditorDialogAsync(BuildEditorState(listModel));
        if (editorResult is null)
        {
            return;
        }

        ApplyEditorResult(listModel, editorResult);

        var normalizedName = string.IsNullOrWhiteSpace(editorResult.Name)
            ? "New Smart List"
            : editorResult.Name.Trim();

        var wasSelected = ReferenceEquals(SelectedSmartListItem, item);
        item.Name = normalizedName;
        SelectedSmartListItem = item;

        if (wasSelected)
        {
            RaiseSmartListFilterChanged(listModel);
        }

        await _settingsService.SaveAsync();
    }

    [RelayCommand]
    private async Task RemoveSmartList(SidebarItem? item)
    {
        if (item is null || item.IsHeader)
        {
            return;
        }

        var parent = FindParent(SmartListItems, item);
        if (parent is null)
        {
            return;
        }

        if (!_settingsMap.TryGetValue(parent, out var parentModel) ||
            !_settingsMap.TryGetValue(item, out var itemModel))
        {
            return;
        }

        if (!RemoveFromCollection(SmartListItems, item))
        {
            return;
        }

        parentModel.Items.RemoveAll(x => x.Id == itemModel.Id);
        DetachAndUnmap(item);

        SelectedSmartListItem = SmartListItems.FirstOrDefault();
        await _settingsService.SaveAsync();
    }

    [RelayCommand]
    private void SortSmartLists()
    {
        SortCollection(SmartListItems);
    }

    private async Task AddSmartListUnderAsync(SidebarItem parent)
    {
        if (!_settingsMap.TryGetValue(parent, out var parentModel))
        {
            return;
        }

        var editorResult = await _dialogService.ShowSmartListEditorDialogAsync(new SmartListEditorResult
        {
            Name = "New Smart List",
            MatchMode = "All",
            Scope = "Library",
            Rules = [new SmartListRuleResult()]
        });
        if (editorResult is null)
        {
            return;
        }

        var newSmartList = new ComicListItem
        {
            Id = Guid.NewGuid(),
            Type = "ComicSmartListItem",
            Name = string.IsNullOrWhiteSpace(editorResult.Name) ? "New Smart List" : editorResult.Name.Trim(),
            BookCount = 0,
            NewBookCount = 0,
            NewBookCountDateUtc = DateTimeOffset.UtcNow,
            Matchers = BuildMatchers(editorResult),
            MatchMode = editorResult.MatchMode
        };

        ApplyExpressionState(newSmartList, editorResult);

        parentModel.Items.Add(newSmartList);

        var newItem = CreateSidebarItem(newSmartList);
        parent.Children.Add(newItem);
        SelectedSmartListItem = newItem;

        await _settingsService.SaveAsync();
    }

    private SmartListEditorResult BuildEditorState(ComicListItem listModel)
    {
        var expression = _smartListExpressionService.ResolveExpression(listModel);
        var rules = new List<SmartListRuleResult>();

        foreach (var child in expression.Children)
        {
            FlattenRule(rules, child, expression.Mode == MatcherMode.Or ? "Any" : "All");
        }

        return new SmartListEditorResult
        {
            Name = listModel.Name,
            Notes = string.Empty,
            MatchMode = expression.Mode == MatcherMode.Or ? "Any" : "All",
            Scope = "Library",
            ShowInQuickOpen = false,
            LimitToBooksEnabled = false,
            LimitBookCount = 25,
            Rules = rules.Count == 0 ? [new SmartListRuleResult()] : rules.ToArray()
        };
    }

    private void ApplyEditorResult(ComicListItem listModel, SmartListEditorResult editorResult)
    {
        listModel.Name = string.IsNullOrWhiteSpace(editorResult.Name)
            ? "New Smart List"
            : editorResult.Name.Trim();
        listModel.MatchMode = string.Equals(editorResult.MatchMode, "Any", StringComparison.OrdinalIgnoreCase)
            ? "Any"
            : "All";
        listModel.Matchers = BuildMatchers(editorResult);
        ApplyExpressionState(listModel, editorResult);
        listModel.NewBookCountDateUtc = DateTimeOffset.UtcNow;
    }

    private void ApplyExpressionState(ComicListItem listModel, SmartListEditorResult editorResult)
    {
        var expression = BuildExpression(editorResult);
        listModel.Expression = _smartListExpressionService.ToSettingsExpression(expression);
        listModel.QueryText = _smartListExpressionService.ToQueryText(expression);
    }

    private static List<ComicBookMatcher> BuildMatchers(SmartListEditorResult editorResult)
    {
        var matchers = new List<ComicBookMatcher>();

        foreach (var rule in editorResult.Rules.Where(x => !x.IsGroupHeader))
        {
            var trimmedValue = rule.Value?.Trim() ?? string.Empty;
            matchers.Add(new ComicBookMatcher
            {
                MatcherType = MapFieldToMatcherType(rule.Field),
                Not = rule.IsNegated,
                MatchOperator = MapOperator(rule.Operator),
                MatchValue = ParseInt(trimmedValue),
                MatchValue2 = null,
                MatchValueText = trimmedValue,
                MatchValueText2 = null
            });
        }

        return matchers;
    }

    private static string MapFieldToMatcherType(string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            return "ComicBookUnknownMatcher";
        }

        var tokenized = new string(fieldName
            .Where(char.IsLetterOrDigit)
            .ToArray());

        if (string.IsNullOrWhiteSpace(tokenized))
        {
            return "ComicBookUnknownMatcher";
        }

        return $"ComicBook{tokenized}Matcher";
    }

    private static int? MapOperator(string op)
    {
        return op.Trim().ToLowerInvariant() switch
        {
            "is" => 1,
            "contains" => 2,
            "contains any of" => 3,
            "contains all of" => 4,
            "starts with" => 5,
            "ends with" => 6,
            "list contains" => 7,
            "regular expression" => 8,
            _ => 2
        };
    }

    private static int? ParseInt(string value)
    {
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private static string ToOperatorName(int? op)
    {
        return op switch
        {
            1 => "is",
            2 => "contains",
            3 => "contains any of",
            4 => "contains all of",
            5 => "starts with",
            6 => "ends with",
            7 => "list contains",
            8 => "regular expression",
            _ => "contains"
        };
    }

    private static string ToFieldName(string matcherType)
    {
        if (string.IsNullOrWhiteSpace(matcherType))
        {
            return "All";
        }

        var token = matcherType
            .Replace("ComicBook", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Matcher", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        if (string.IsNullOrWhiteSpace(token))
        {
            return "All";
        }

        var chars = new List<char>(token.Length * 2);
        for (var i = 0; i < token.Length; i++)
        {
            var current = token[i];
            var shouldAddSpace = i > 0 &&
                                 char.IsUpper(current) &&
                                 (char.IsLower(token[i - 1]) ||
                                  (i + 1 < token.Length && char.IsLower(token[i + 1])));

            if (shouldAddSpace)
            {
                chars.Add(' ');
            }

            chars.Add(current);
        }

        return new string(chars.ToArray());
    }

    private static MatcherGroupNode BuildExpression(SmartListEditorResult editorResult)
    {
        var root = new MatcherGroupNode
        {
            Mode = string.Equals(editorResult.MatchMode, "Any", StringComparison.OrdinalIgnoreCase)
                ? MatcherMode.Or
                : MatcherMode.And
        };

        MatcherGroupNode? activeGroup = null;
        foreach (var rule in editorResult.Rules)
        {
            if (rule.IsGroupHeader)
            {
                activeGroup = new MatcherGroupNode
                {
                    Mode = string.Equals(rule.MatchMode, "Any", StringComparison.OrdinalIgnoreCase)
                        ? MatcherMode.Or
                        : MatcherMode.And,
                    Not = rule.IsNegated
                };

                root.Children.Add(activeGroup);
                continue;
            }

            var matcherRule = new MatcherRuleNode
            {
                Field = SmartListNodeMapper.ParseField(rule.Field),
                Operator = SmartListNodeMapper.ParseOperator(rule.Operator),
                Value1 = rule.Value?.Trim(),
                ValueKind = InferValueKind(rule.Field, rule.Value),
                Not = rule.IsNegated
            };

            if (activeGroup is null)
            {
                root.Children.Add(matcherRule);
            }
            else
            {
                activeGroup.Children.Add(matcherRule);
            }
        }

        return root;
    }

    private static void FlattenRule(List<SmartListRuleResult> rows, IMatcherNode node, string defaultMatchMode)
    {
        switch (node)
        {
            case MatcherRuleNode rule:
                rows.Add(new SmartListRuleResult
                {
                    IsGroupHeader = false,
                    IsNegated = rule.Not,
                    MatchMode = defaultMatchMode,
                    Field = SmartListNodeMapper.ToFieldName(rule.Field),
                    Operator = SmartListNodeMapper.ToOperatorName(rule.Operator),
                    Value = rule.Value1 ?? string.Empty
                });
                return;
            case MatcherGroupNode group:
                rows.Add(new SmartListRuleResult
                {
                    IsGroupHeader = true,
                    IsNegated = group.Not,
                    MatchMode = group.Mode == MatcherMode.Or ? "Any" : "All",
                    Field = "of the following rules:",
                    Operator = string.Empty,
                    Value = string.Empty
                });

                foreach (var child in group.Children)
                {
                    FlattenRule(rows, child, group.Mode == MatcherMode.Or ? "Any" : "All");
                }

                return;
        }
    }

    private static MatcherValueKind InferValueKind(string field, string? value)
    {
        var parsedField = SmartListNodeMapper.ParseField(field);
        if (parsedField is MatcherField.SizeBytes or MatcherField.Year)
        {
            return MatcherValueKind.Number;
        }

        if (parsedField is MatcherField.Added or MatcherField.Modified or MatcherField.LastScanned)
        {
            return MatcherValueKind.Date;
        }

        return double.TryParse(value, out _)
            ? MatcherValueKind.Number
            : MatcherValueKind.String;
    }

    private async void SmartListItemOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not SidebarItem item || item.IsHeader)
        {
            return;
        }

        if (e.PropertyName != nameof(SidebarItem.IsEditing) || item.IsEditing)
        {
            return;
        }

        if (!_settingsMap.TryGetValue(item, out var itemModel))
        {
            return;
        }

        var normalizedName = string.IsNullOrWhiteSpace(item.Name)
            ? itemModel.Name
            : item.Name.Trim();

        if (string.Equals(itemModel.Name, normalizedName, System.StringComparison.Ordinal))
        {
            return;
        }

        itemModel.Name = normalizedName;
        item.Name = normalizedName;
        await _settingsService.SaveAsync();
    }

    private void DetachAndUnmap(SidebarItem item)
    {
        item.PropertyChanged -= SmartListItemOnPropertyChanged;
        _settingsMap.Remove(item);

        foreach (var child in item.Children)
        {
            DetachAndUnmap(child);
        }
    }

    private void RaiseAllComicsFilterChanged()
    {
        ActiveFilterChanged?.Invoke(this, new ComicGridFilterRequest
        {
            Mode = ComicGridFilterMode.AllComics,
            SmartListName = "All Comics"
        });
    }

    private void RaiseSmartListFilterChanged(ComicListItem model)
    {
        ActiveFilterChanged?.Invoke(this, new ComicGridFilterRequest
        {
            Mode = ComicGridFilterMode.SmartList,
            SmartListId = model.Id,
            SmartListName = model.Name,
            SmartList = model
        });
    }

    private static void SortCollection(ObservableCollection<SidebarItem> items)
    {
        var sorted = items
            .OrderByDescending(x => x.IsHeader)
            .ThenBy(x => x.Name, System.StringComparer.OrdinalIgnoreCase)
            .ToList();

        items.Clear();
        foreach (var item in sorted)
        {
            items.Add(item);
            if (item.Children.Count > 0)
            {
                SortCollection(item.Children);
            }
        }
    }

    private static bool RemoveFromCollection(ObservableCollection<SidebarItem> source, SidebarItem target)
    {
        if (source.Remove(target))
        {
            return true;
        }

        foreach (var item in source)
        {
            if (RemoveFromCollection(item.Children, target))
            {
                return true;
            }
        }

        return false;
    }

    private static SidebarItem? FindParent(ObservableCollection<SidebarItem> source, SidebarItem target)
    {
        foreach (var item in source)
        {
            if (item.Children.Contains(target))
            {
                return item;
            }

            var nested = FindParent(item.Children, target);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private ObservableCollection<SidebarItem> BuildSmartListItems(AppSettings settings)
    {
        var folderLists = settings.ComicLists
            .Where(x => string.Equals(x.Type, "ComicListItemFolder", System.StringComparison.OrdinalIgnoreCase))
            .Select(x => CreateSidebarItem(x, isRoot: true))
            .ToList();

        return [.. folderLists];
    }

    private SidebarItem CreateSidebarItem(ComicListItem item, bool isRoot = false)
    {
        var isHeader = isRoot || string.Equals(item.Type, "ComicListItemFolder", System.StringComparison.OrdinalIgnoreCase);
        int? count = item.BookCount > 0 ? item.BookCount : null;
        var sidebarItem = new SidebarItem(item.Name, count, isHeader: isHeader);

        _settingsMap[sidebarItem] = item;
        sidebarItem.PropertyChanged += SmartListItemOnPropertyChanged;

        foreach (var child in item.Items)
        {
            sidebarItem.Children.Add(CreateSidebarItem(child));
        }

        return sidebarItem;
    }

    private SidebarItem? GetDefaultSmartListRoot()
    {
        var namedRoot = SmartListItems.FirstOrDefault(x =>
            x.IsHeader && string.Equals(x.Name, "Smart Lists", StringComparison.OrdinalIgnoreCase));

        if (namedRoot is not null)
        {
            return namedRoot;
        }

        var firstHeader = SmartListItems.FirstOrDefault(x => x.IsHeader);
        if (firstHeader is not null)
        {
            return firstHeader;
        }

        return SmartListItems.FirstOrDefault();
    }

    private async Task LoadLibraryCountAsync()
    {
        await _comicDatabaseService.InitializeAsync();
        var totalCount = await _scanRepository.GetTotalCountAsync();
        Interlocked.Exchange(ref _libraryCountBaseline, totalCount);

        Dispatcher.UIThread.Post(() =>
        {
            _allComicsItem.Count = totalCount;
        });
    }

    private void OnScanProgressChanged(object? sender, Engine.Models.ScanProgressUpdate update)
    {
        var baseline = Volatile.Read(ref _libraryCountBaseline);
        var currentCount = baseline + update.FilesInserted;
        if (currentCount < 0)
        {
            currentCount = 0;
        }

        Dispatcher.UIThread.Post(() =>
        {
            _allComicsItem.Count = (int)currentCount;
        });
    }

    private void OnScanStateChanged(object? sender, Engine.Models.ScanStateChangedEventArgs eventArgs)
    {
        if (eventArgs.IsRunning)
        {
            var currentCount = _allComicsItem.Count ?? 0;
            Interlocked.Exchange(ref _libraryCountBaseline, currentCount);
            return;
        }

        _ = LoadLibraryCountAsync();
    }
}
