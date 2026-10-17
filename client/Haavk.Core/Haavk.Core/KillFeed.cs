using System;
using System.Collections.Generic;

namespace Haavk.Core;

public sealed class KillFeed
{
	private struct Queued
	{
		public bool Head;

		public string Name;

		public float Meters;

		public double Arrival;
	}

	public const int MaxSkulls = 8;

	public const int MaxPending = 4;

	public const float SlideSeconds = 0.2f;

	public const float MinTextSeconds = 1.55f;

	public const float TextGap = 0.167f;

	private readonly List<KillSkull> _skulls = new List<KillSkull>();

	private readonly List<KillText> _texts = new List<KillText>();

	private readonly Queue<Queued> _pending = new Queue<Queued>();

	private double _lastAdd = double.NegativeInfinity;

	public IReadOnlyList<KillSkull> Skulls => _skulls;

	public IReadOnlyList<KillText> Texts => _texts;

	public int Pending => _pending.Count;

	public bool Active
	{
		get
		{
			if (_skulls.Count <= 0 && _texts.Count <= 0)
			{
				return _pending.Count > 0;
			}
			return true;
		}
	}

	private KillText Current
	{
		get
		{
			if (_texts.Count != 0)
			{
				return _texts[_texts.Count - 1];
			}
			return null;
		}
	}

	public void Add(bool head, string name, float meters, double now)
	{
		Update(now);
		_lastAdd = now;
		KillSkull killSkull = new KillSkull(head, now);
		_skulls.Insert(0, killSkull);
		if (_skulls.Count > 8)
		{
			_skulls.RemoveAt(_skulls.Count - 1);
		}
		Relayout(now, killSkull);
		KillText current = Current;
		if (_pending.Count == 0 && (current == null || current.End <= now))
		{
			_texts.Add(new KillText(head, name, meters, now));
		}
		else
		{
			_pending.Enqueue(new Queued
			{
				Head = head,
				Name = name,
				Meters = meters,
				Arrival = now
			});
			if (_pending.Count > 4)
			{
				_pending.Dequeue();
			}
		}
		Update(now);
	}

	public void Update(double now)
	{
		bool flag = false;
		for (int num = _skulls.Count - 1; num >= 0; num--)
		{
			if (now >= _skulls[num].Start + 3.0999999046325684 + 0.10000000149011612)
			{
				_skulls.RemoveAt(num);
				flag = true;
			}
		}
		if (flag)
		{
			Relayout(now);
		}
		while (true)
		{
			KillText current = Current;
			if (current != null && current.End > now)
			{
				current.End = EndOf(current);
			}
			if (_pending.Count == 0)
			{
				break;
			}
			double num2 = ((current == null) ? now : (current.End + 0.16699999570846558 - 0.1899999976158142));
			if (now < num2)
			{
				break;
			}
			Queued queued = _pending.Dequeue();
			_texts.Add(new KillText(queued.Head, queued.Name, queued.Meters, num2));
		}
		for (int num3 = _texts.Count - 1; num3 >= 0; num3--)
		{
			KillText killText = _texts[num3];
			if (now >= killText.End + 0.06700000166893005 + 0.10000000149011612)
			{
				_texts.RemoveAt(num3);
			}
		}
	}

	public void Clear()
	{
		_skulls.Clear();
		_texts.Clear();
		_pending.Clear();
		_lastAdd = double.NegativeInfinity;
	}

	private double EndOf(KillText x)
	{
		double val = x.Start + 0.1899999976158142 + 1.5499999523162842;
		double val2 = ((_pending.Count > 0) ? _pending.Peek().Arrival : (_lastAdd + 3.0999999046325684));
		return Math.Max(val, val2);
	}

	private void Relayout(double now, KillSkull fresh = null)
	{
		int count = _skulls.Count;
		for (int i = 0; i < count; i++)
		{
			KillSkull killSkull = _skulls[i];
			float num = (float)i - (float)(count - 1) * 0.5f;
			killSkull.From = ((killSkull == fresh) ? num : killSkull.SlotAt(now));
			killSkull.To = num;
			killSkull.MoveStart = now;
		}
	}
}
