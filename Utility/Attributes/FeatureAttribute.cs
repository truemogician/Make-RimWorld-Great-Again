using System;

namespace TrueMogician.RimWorld.Utility.Attributes;

public abstract class FeatureAttributeBase : Attribute { }

/// <summary>
///     Configure a feature flag enum field for use in a <see cref="FeatureSettings{T}" />.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class FeatureAttribute : FeatureAttributeBase {
	public FeatureAttribute() { }

	public FeatureAttribute(string translationKey, bool? defaultEnabled = null) {
		TranslationKey = translationKey;
		DefaultEnabled = defaultEnabled;
	}

	/// <summary>
	///     The translation key prefix for this feature. Used to look up labels (${<see cref="TranslationKey" />}.label) and
	///     descriptions
	///     (${<see cref="TranslationKey" />}.description).
	/// </summary>
	public string? TranslationKey { get; init; }

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
}

/// <summary>
///     Configure a feature flag enum for use in a <see cref="FeatureSettings{T}" />.
/// </summary>
[AttributeUsage(AttributeTargets.Enum)]
public class FeaturesEnumAttribute : FeatureAttributeBase {
	public FeaturesEnumAttribute() { }

	public FeaturesEnumAttribute(string translationKey, bool defaultEnabled = true) {
		TranslationKey = translationKey;
		DefaultEnabled = defaultEnabled;
	}

	/// <summary>
	///     The translation key prefix for this feature enum. The default key prefix for each feature will be
	///		${<see cref="TranslationKey" />}.${featureName} if set.
	///     Per-feature translation keys can be overridden using <see cref="FeatureAttribute.TranslationKey" />.
	/// </summary>
	public string? TranslationKey { get; init; }

	/// <summary>
	///     Whether features are enabled by default. This can be overridden on a per-feature basis using
	///     <see cref="FeatureAttribute.DefaultEnabled" />.
	/// </summary>
	/// <value></value>
	public bool DefaultEnabled { get; init; } = true;
}

/// <summary>
///     Indicates that a feature flag enum field should be ignored in the settings UI.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class FeatureIgnoreAttribute : FeatureAttributeBase { }