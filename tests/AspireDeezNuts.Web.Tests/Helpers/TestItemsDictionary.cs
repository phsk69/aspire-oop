using System.Collections;

namespace AspireDeezNuts.Web.Tests.Helpers;

public class TestItemsDictionary : IDictionary<object, object?>
{
    private readonly Dictionary<object, object?> _inner = [];

    public object? this[object key] 
    { 
        get => _inner.TryGetValue(key, out var value) ? value : null;
        set => _inner[key] = value;
    }

    public ICollection<object> Keys => _inner.Keys;
    public ICollection<object?> Values => _inner.Values;
    public int Count => _inner.Count;
    public bool IsReadOnly => false;

    public void Add(object key, object? value) => _inner.Add(key, value);
    public void Add(KeyValuePair<object, object?> item) => _inner.Add(item.Key, item.Value);
    public void Clear() => _inner.Clear();
    public bool Contains(KeyValuePair<object, object?> item) => _inner.Contains(item);
    public bool ContainsKey(object key) => _inner.ContainsKey(key);
    public void CopyTo(KeyValuePair<object, object?>[] array, int arrayIndex) => ((IDictionary<object, object?>)_inner).CopyTo(array, arrayIndex);
    public IEnumerator<KeyValuePair<object, object?>> GetEnumerator() => _inner.GetEnumerator();
    public bool Remove(object key) => _inner.Remove(key);
    public bool Remove(KeyValuePair<object, object?> item) => ((IDictionary<object, object?>)_inner).Remove(item);
    public bool TryGetValue(object key, out object? value) => _inner.TryGetValue(key, out value);
    IEnumerator IEnumerable.GetEnumerator() => _inner.GetEnumerator();
}