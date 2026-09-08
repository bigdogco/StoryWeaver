using System.Collections.ObjectModel;
using StoryWeaver.Desktop.Presentation;

namespace StoryWeaver.Desktop.ViewModels;

public sealed class EntityTabViewModel(string title, EntityKind kind) : ObservableObject
{
    private EntityDetails? _selected;
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
        }
    }

    public void Replace(IEnumerable<EntityDetails> entities)
    {
        var reference = Selected?.Reference;
        Selected = null;
        Entities.Clear();
        foreach (var entity in entities) Entities.Add(entity);
        if (reference is not null) Selected = Entities.FirstOrDefault(entity => entity.Reference == reference);
        Raise(nameof(IsEmpty));
    }
}
