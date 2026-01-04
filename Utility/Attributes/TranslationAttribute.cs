using System;

namespace TrueMogician.RimWorld.Utility.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Field | AttributeTargets.Property)]
public class TranslationAttribute : Attribute {
	public TranslationAttribute() { }

	public TranslationAttribute(string key) => Key = key;

	public string? Prefix { get; init; }

	public string? Key { get; init; }

	public bool ImplicitMembers { get; init; } = false;
}