using System.Collections.ObjectModel;
using StoryWeaver.Desktop.Presentation;

namespace StoryWeaver.Desktop.ViewModels;

public sealed class EntityTabViewModel(string title, EntityKind kind) : ObservableObject
{
    private EntityDetails? _selected;
    private bool _canEdit;
    public bool CanEdit { get => _canEdit; set { if (Set(ref _canEdit, value)) { Raise(nameof(CanRemove)); Raise(nameof(CanEditSelected)); Raise(nameof(ShowPlayerRemovalHint)); } } }
    public bool ShowPlayerRemovalHint => CanEdit && IsPlayer;
    public string AddLabel => $"Add {Kind.ToString().ToLowerInvariant()}";
    public bool IsPlayer => Kind == EntityKind.Character && Selected is { } entry
        && (string.Equals(entry.Reference.Id, "player", StringComparison.OrdinalIgnoreCase)
            || string.Equals(entry.CanonKey, "player", StringComparison.OrdinalIgnoreCase));
    public bool CanEditSelected => CanEdit && Selected?.CanAuthor == true;
    public bool CanRemove => CanEditSelected && !IsPlayer;
    public string RemoveHint => IsPlayer ? "The playthrough needs its player character. Use Edit to correct them." : "Preview removal consequences";
    public string Title { get; } = title;
    public EntityKind Kind { get; } = kind;
    public ObservableCollection<EntityDetails> Entities { get; } = [];
    public bool IsEmpty => Entities.Count == 0;
    public bool IsListVisible => Selected is null;
    public bool IsDetailVisible => Selected is not null;
    public EntityDetails? Selected
    {
        get => _selected;
        set
        {
            if (!Set(ref _selected, value)) return;
            Raise(nameof(IsListVisible));
            Raise(nameof(IsDetailVisible));
            Raise(nameof(IsPlayer)); Raise(nameof(CanRemove)); Raise(nameof(RemoveHint));
            Raise(nameof(ShowPlayerRemovalHint));
            Raise(nameof(CanEditSelected));
        }
    }

    public void Replace(IEnumerable<EntityDetails> entities)
    {
        var reference = Selected?.Reference;
        var key = Selected?.CanonKey;
        Selected = null;
        Entities.Clear();
        foreach (var entity in entities) Entities.Add(entity);
        if (reference is not null) Selected = Entities.FirstOrDefault(entity => entity.Reference == reference && entity.CanonKey == key);
        Raise(nameof(IsEmpty));
    }
}
