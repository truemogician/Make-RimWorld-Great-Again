global using IProfilerRecord = TrueMogician.RimWorld.Profiler.IProfilerRecord<string>;
global using ProfilerRecord = TrueMogician.RimWorld.Profiler.ProfilerRecord<string>;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;

namespace TrueMogician.RimWorld.Profiler;

public interface IProfilerRecord<out T> {
	T Key { get; }

	int HitCount { get; }

	long QpcTicks { get; }

	public void Reset();
}

public interface ISingleProfilerRecord<out T> : IProfilerRecord<T> {
	void Increment(long qpcTicks);
}

public interface IProfilerRecordFactory<out TRecord, in TKey> where TRecord : IProfilerRecord<TKey> {
	TRecord Create(TKey key);
}

public struct ProfilerRecord<T>(T key, int hitCount = 0, long qpcTicks = 0) : ISingleProfilerRecord<T> {
	public T Key { get; } = key;

	public int HitCount { get; private set; } = hitCount;

	public long QpcTicks { get; private set; } = qpcTicks;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Increment(long qpcTicks) {
		++HitCount;
		QpcTicks += qpcTicks;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Reset() {
		HitCount = 0;
		QpcTicks = 0;
	}

	public void Deconstruct(out T key, out int hitCount, out long qpcTicks) {
		key = Key;
		hitCount = HitCount;
		qpcTicks = QpcTicks;
	}

	public class Factory : IProfilerRecordFactory<ProfilerRecord<T>, T> {
		public static Factory Inst { get; } = new();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ProfilerRecord<T> Create(T key) => new(key);
	}
}

public class ProfilerRecordKeyEqualityComparer<TRecord, TKey>(IEqualityComparer<TKey> keyComparer) : IEqualityComparer<TRecord>
	where TRecord : IProfilerRecord<TKey> {
	public ProfilerRecordKeyEqualityComparer() : this(EqualityComparer<TKey>.Default) { }

	public static ProfilerRecordKeyEqualityComparer<TRecord, TKey> Default { get; } = new();

	public IEqualityComparer<TKey> KeyComparer { get; } = keyComparer;

	public bool Equals(TRecord? x, TRecord? y) {
		if (ReferenceEquals(x, y))
			return true;
		if (x is null || y is null)
			return false;
		return KeyComparer.Equals(x.Key, y.Key);
	}

	public int GetHashCode(TRecord obj) => KeyComparer.GetHashCode(obj.Key);
}

public class AggProfilerRecord<TKey, TRecord, TRecordKey>(TKey key, IEqualityComparer<TRecordKey>? comparer = null)
	: IProfilerRecord<TKey>, ICollection<TRecord>
	where TRecord : IProfilerRecord<TRecordKey> {
	protected readonly Dictionary<TRecordKey, TRecord> Dict = comparer is null ? new() : new(comparer);

	public TKey Key { get; } = key;

	public int HitCount => Dict.Values.Sum(r => r.HitCount);

	public long QpcTicks => Dict.Values.Sum(r => r.QpcTicks);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Reset() => Dict.Clear();

	public IEnumerator<TRecord> GetEnumerator() => Dict.Values.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Add(TRecord item) => Dict.Add(item.Key, item);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Clear() => Dict.Clear();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Contains(TRecord item) => Dict.ContainsKey(item.Key);

	public void CopyTo(TRecord[] array, int arrayIndex) {
		if (array.Length - arrayIndex < Dict.Count)
			throw new ArgumentOutOfRangeException(nameof(arrayIndex));
		foreach (var record in Dict.Values)
			array[arrayIndex++] = record;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Remove(TRecord item) => Dict.Remove(item.Key);

	public int Count => Dict.Count;

	public bool IsReadOnly => false;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryGetValue(TRecordKey key, [MaybeNullWhen(false)] out TRecord record)
		=> Dict.TryGetValue(key, out record);
}

public class AggProfilerRecord<TKey, TRecordKey>(
	TKey key,
	IProfilerRecordFactory<ProfilerRecord<TRecordKey>, TRecordKey> factory,
	IEqualityComparer<TRecordKey>? comparer = null
) : AggProfilerRecord<TKey, ProfilerRecord<TRecordKey>, TRecordKey>(key, comparer) {
	public void Increment(TRecordKey key, int qpcTicks) {
		if (!Dict.TryGetValue(key, out var record))
			Dict[key] = record = factory.Create(key);
		record.Increment(qpcTicks);
	}
}