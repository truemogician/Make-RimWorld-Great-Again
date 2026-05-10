using System;
using System.Collections.Generic;
using Verse;

namespace TrueMogician.RimWorld.Utility;

public interface ITranslationProvider {
	public TaggedString Translate(string key, params NamedArgument[] args);
}

public class TranslationProvider(string? prefix = null) : ITranslationProvider {
	public TaggedString Translate(string key, params NamedArgument[] args) {
		if (KeyTransformer?.Invoke(key) is { } newKey)
			key = newKey;
		var fullKey = Prefix is null ? key : $"{Prefix}.{key}";
		return args.Length == 0 ? fullKey.Translate() : fullKey.Translate(args);
	}

	public string? Prefix { get; init; } = prefix;

	public Func<string, string?>? KeyTransformer { get; init; }

	public IReadOnlyDictionary<string, string> KeyMap {
		init => KeyTransformer = value.GetValueOrDefault;
	}
}