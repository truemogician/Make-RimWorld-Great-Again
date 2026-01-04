using System;

namespace TrueMogician.RimWorld.Utility.Attributes;

public abstract class FeatureAttributeBase : Attribute { }

/// <summary>
///     Configure a feature flag enum field for use in a <see cref="FeatureSettings{T}" />.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class FeatureAttribute : FeatureAttributeBase {
	public FeatureAttribute() { }

	public FeatureAttribute(bool? defaultEnabled) {
		DefaultEnabled = defaultEnabled;
	}

	/// <summary>
	///     The fallback label for this feature if no translation is found.
	/// </summary>
	public string? Label { get; init; }

	/// <summary>
	///     The fallback description for this feature if no translation is found.
	/// </summary>
	public string? Description { get; init; }

	/// <summary>
	///     Whether this feature is enabled by default. If null, the default from
	///     <see cref="FeaturesEnumAttribute.DefaultEnabled" /> is used.
	/// </summary>
	public bool? DefaultEnabled { get; init; }

	/// <summary>
	///		An array of mod identifiers that this feature depends on.
	/// </summary>
	public string[]? ModDependencies { get; init; }

	/// <summary>
	///		An array of mod identifiers that this feature is incompatible with.
	/// </summary>
	public string[]? ModIncompatibilities { get; init; }
}

/// <summary>
///     Configure a feature flag enum for use in a <see cref="FeatureSettings{T}" />.
/// </summary>
[AttributeUsage(AttributeTargets.Enum)]
public class FeaturesEnumAttribute(bool defaultEnabled = true) : FeatureAttributeBase {
	/// <summary>
	///     Whether features are enabled by default. This can be overridden on a per-feature basis using
	///     <see cref="FeatureAttribute.DefaultEnabled" />.
	/// </summary>
	/// <value></value>
	public bool DefaultEnabled { get; init; } = defaultEnabled;
}

/// <summary>
///     Indicates that a feature flag enum field should be ignored in the settings UI.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class FeatureIgnoreAttribute : FeatureAttributeBase;