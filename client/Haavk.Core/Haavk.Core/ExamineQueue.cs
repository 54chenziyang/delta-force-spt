using System.Collections.Generic;

namespace Haavk.Core;

public sealed class ExamineQueue<T> where T : class
{
	private readonly Queue<T> _items = new Queue<T>();

	private readonly HashSet<string> _seenIds = new HashSet<string>();

	public int Count => _items.Count;

	public bool Enqueue(string id, T item)
	{
		if (string.IsNullOrEmpty(id) || item == null)
		{
			return false;
		}
		if (!_seenIds.Add(id))
		{
			return false;
		}
		_items.Enqueue(item);
		return true;
	}

	public bool TryDequeue(out T item)
	{
		if (_items.Count == 0)
		{
			item = null;
			return false;
		}
		item = _items.Dequeue();
		return true;
	}

	public void Clear()
	{
		_items.Clear();
		_seenIds.Clear();
	}
}
