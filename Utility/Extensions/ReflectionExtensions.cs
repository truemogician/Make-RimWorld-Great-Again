using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TrueMogician.RimWorld.Utility.Attributes;
using Verse;

namespace TrueMogician.RimWorld.Utility.Extensions;

public static class ReflectionExtensions {
	private static readonly Dictionary<MemberInfo, string?> _translationKeyCache = new(MemberInfoComparer.Default);

	public class MemberInfoComparer : IEqualityComparer<MemberInfo> {
		public bool Equals(MemberInfo? x, MemberInfo? y) {
			if (x is null && y is null)
				return true;
			if (x is null || y is null)
				return false;
			return x.MetadataToken == y.MetadataToken && x.Module == y.Module;
		}

		public int GetHashCode(MemberInfo obj) => HashCode.Combine(obj.MetadataToken, obj.Module);

		public static MemberInfoComparer Default { get; } = new();
	}

	public static string? GetTranslationKey(this MemberInfo member) {
		if (!_translationKeyCache.TryGetValue(member, out var key))
			_translationKeyCache[member] = key = GetTranslationKeyPrivate(member);
		return key;
	}

	public static string? TranslateMember(this Type type, string memberName, string? subField = null) {
		var member = type.GetMember(memberName).FirstOrDefault();
		if (member is null)
			throw new ArgumentException($"No such property: {memberName}");
		string? key = member.GetTranslationKey();
		if (key is not null && subField is not null)
			key = $"{key}.{subField}";
		return key.TryTranslate(out var translation) ? translation : null;
	}

	private static string? GetTranslationKeyPrivate(MemberInfo member) {
		var attr = member.GetCustomAttribute<TranslationAttribute>();
		var parent = member.DeclaringType;
		if (attr is null) {
			return parent?.GetCustomAttribute<TranslationAttribute>() is { ImplicitMembers: true }
				? $"{GetTranslationKeyPrivate(parent)}.{member.Name}"
				: null;
		}
		if (attr.Key is { } k)
			return k;
		var key = attr.Prefix is { } prefix ? $"{prefix}.{member.Name}" : member.Name;
		if (parent is not null && GetTranslationKeyPrivate(parent) is { } parentPrefix)
			key = $"{parentPrefix}.{key}";
		return key;
	}
}