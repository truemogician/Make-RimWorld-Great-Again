using System;

namespace TrueMogician.RimWorld.Utility.Attributes;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Enum | AttributeTargets.Class | AttributeTargets.Struct)]
public class LabelAttribute(string label) : Attribute {
	public string Label { get; } = label;

	public override string ToString() => Label;

	public static implicit operator string(LabelAttribute attribute) => attribute.Label;
}