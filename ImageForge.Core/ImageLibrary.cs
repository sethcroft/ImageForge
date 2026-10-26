using System.ComponentModel;

namespace ImageForge.Core;

public enum LibraryNodeKind { Category, Folder, Source, Edition, Message }
public sealed record LibrarySource<T>(string Path, IReadOnlyList<T> Editions) where T : class;

public sealed class LibraryNode<T>(string key, string name, string path, LibraryNodeKind kind, T? edition = null) : INotifyPropertyChanged where T : class
{
    public string Key { get; } = key;
    public string Name { get; } = name;
    public string Path { get; } = path;
    public LibraryNodeKind Kind { get; } = kind;
    public T? Edition { get; } = edition;
    public bool IsCategory => Kind == LibraryNodeKind.Category;
    public bool IsMessage => Kind == LibraryNodeKind.Message;
    public string Glyph => Kind switch { LibraryNodeKind.Folder => "📁", LibraryNodeKind.Source => "◉", LibraryNodeKind.Edition => "▤", _ => "" };
    public List<LibraryNode<T>> Children { get; } = [];
    private bool expanded = true, selected;
    public bool IsExpanded { get => expanded; set { if (expanded == value) return; expanded = value; Changed(nameof(IsExpanded)); } }
    public bool IsSelected { get => selected; set { if (selected == value) return; selected = value; Changed(nameof(IsSelected)); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed(string property) => PropertyChanged?.Invoke(this, new(property));
}

public static class ImageLibrary
{
    public static string Category(string path) => System.IO.Path.GetExtension(path).ToLowerInvariant() is ".esd" or ".swm" ? "Read-only" : "Other";
    public static IReadOnlyList<LibraryNode<T>> Build<T>(IEnumerable<LibrarySource<T>> sources, Func<T,int> index, Func<T,string> name, string query = "", string category = "All") where T : class
    {
        var roots = new[] { "Read-only", "Other" }.Where(c => category == "All" || c == category)
            .Select(c => new LibraryNode<T>("category:" + c, c, "", LibraryNodeKind.Category)).ToArray();
        foreach (var root in roots)
        {
            var folders = new Dictionary<string,LibraryNode<T>>(StringComparer.OrdinalIgnoreCase);
            var visible = sources.Where(source => Category(source.Path) == root.Name)
                .Select(source => new LibrarySource<T>(Path.GetFullPath(source.Path), source.Editions.Where(edition => string.IsNullOrWhiteSpace(query) ||
                    (source.Path + " " + name(edition) + " " + Path.GetExtension(source.Path)).Contains(query.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray()))
                .Where(source => source.Editions.Count > 0).OrderBy(source => source.Path, StringComparer.OrdinalIgnoreCase).ToArray();
            var anchors = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            foreach (var drive in visible.GroupBy(source => Path.GetPathRoot(source.Path)!, StringComparer.OrdinalIgnoreCase))
            {
                var anchor = Path.GetDirectoryName(drive.First().Path)!;
                foreach (var source in drive)
                {
                    var parentPath = Path.GetDirectoryName(source.Path)!;
                    while (!parentPath.Equals(anchor, StringComparison.OrdinalIgnoreCase) && !parentPath.StartsWith(anchor.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        anchor = Path.GetDirectoryName(anchor) ?? drive.Key;
                }
                anchors.Add(drive.Key, anchor);
            }
            foreach (var source in visible)
            {
                var path = source.Path;
                var editions = source.Editions;
                var anchor = anchors[Path.GetPathRoot(path)!];
                var parents = new Stack<string>();
                for (var folder = Path.GetDirectoryName(path); !string.IsNullOrEmpty(folder) && !folders.ContainsKey(folder); folder = Path.GetDirectoryName(folder))
                {
                    parents.Push(folder);
                    if (folder.Equals(anchor, StringComparison.OrdinalIgnoreCase)) break;
                }
                var parent = root;
                if (parents.Count > 0 && Path.GetDirectoryName(parents.Peek()) is { } existing && folders.TryGetValue(existing, out var found)) parent = found;
                else if (parents.Count == 0 && Path.GetDirectoryName(path) is { } direct && folders.TryGetValue(direct, out var directParent)) parent = directParent;
                while (parents.TryPop(out var folder))
                {
                    var caption = folder.Equals(anchor, StringComparison.OrdinalIgnoreCase) ? folder : Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar));
                    var node = new LibraryNode<T>("folder:" + root.Name + ":" + folder, string.IsNullOrEmpty(caption) ? folder : caption, folder, LibraryNodeKind.Folder);
                    folders.Add(folder, node); parent.Children.Add(node); parent = node;
                }
                var file = new LibraryNode<T>("source:" + path, Path.GetFileName(path), path, LibraryNodeKind.Source); parent.Children.Add(file);
                foreach (var edition in editions.OrderBy(index))
                    file.Children.Add(new("edition:" + path + ":" + index(edition), $"{index(edition)} · {name(edition)}", path, LibraryNodeKind.Edition, edition));
            }
            if (root.Children.Count == 0)
                root.Children.Add(new("empty:" + root.Name, string.IsNullOrWhiteSpace(query) ? root.Name == "Read-only" ? "No packed ESD or split SWM sources added." : "No editable image sources added." : "No matching images.", "", LibraryNodeKind.Message));
        }
        return roots;
    }
    public static IEnumerable<LibraryNode<T>> Nodes<T>(IEnumerable<LibraryNode<T>> roots) where T : class
    {
        var stack = new Stack<LibraryNode<T>>(roots.Reverse());
        while (stack.TryPop(out var node))
        {
            yield return node;
            for (var i = node.Children.Count - 1; i >= 0; i--) stack.Push(node.Children[i]);
        }
    }
}
