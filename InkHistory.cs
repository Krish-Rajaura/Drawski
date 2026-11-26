using System.Windows.Ink;

namespace Drawski;

// One history can track several collections (all pages of a pdf)
public class InkHistory
{
    public bool Dirty;
    public Action onChanged;

    readonly Stack<List<Change>> __undo = new(), __redo = new();
    List<Change> __batch;
    bool __applying;

    public void Track(StrokeCollection strokes) => strokes.StrokesChanged += OnStrokesChanged;

    public void Batch(Action action)
    {
        __batch = new List<Change>();
        action();
        if (__batch.Count > 0) Push(__batch);
        __batch = null;
    }

    public void Undo()
    {
        if (__undo.Count == 0) return;

        List<Change> changes = __undo.Pop();
        __applying = true;
        for (int i = changes.Count - 1; i >= 0; i--)
            Apply(changes[i].Owner, changes[i].Removed, changes[i].Added);
        __applying = false;
        __redo.Push(changes);
    }

    public void Redo()
    {
        if (__redo.Count == 0) return;

        List<Change> changes = __redo.Pop();
        __applying = true;
        foreach (Change change in changes)
            Apply(change.Owner, change.Added, change.Removed);
        __applying = false;
        __undo.Push(changes);
    }

    void OnStrokesChanged(object sender, StrokeCollectionChangedEventArgs e)
    {
        StrokeCollection added = Tracked(e.Added), removed = Tracked(e.Removed);
        if (added.Count == 0 && removed.Count == 0) return;

        Dirty = true;
        onChanged?.Invoke();
        if (__applying) return;

        Change change = new((StrokeCollection)sender, added, removed);
        if (__batch != null) __batch.Add(change);
        else Push(new List<Change> { change });
    }

    void Push(List<Change> changes)
    {
        __undo.Push(changes);
        __redo.Clear();
    }

    StrokeCollection Tracked(StrokeCollection strokes) => new(strokes.Where(stroke => !InkSurface.IsLaser(stroke)));

    void Apply(StrokeCollection owner, StrokeCollection toAdd, StrokeCollection toRemove)
    {
        foreach (Stroke stroke in toRemove)
            if (owner.Contains(stroke)) owner.Remove(stroke);
        foreach (Stroke stroke in toAdd)
            if (!owner.Contains(stroke)) owner.Add(stroke);
    }

    readonly record struct Change(StrokeCollection Owner, StrokeCollection Added, StrokeCollection Removed);
}
