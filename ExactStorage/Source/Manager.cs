using System.Runtime.CompilerServices;
using RimWorld;

namespace TrueMogician.RimWorld.ExactStorage;

public static class Manager {
	private static readonly ConditionalWeakTable<StorageSettings, Profile> _profiles = new();

	public static Profile GetProfile(StorageSettings settings) => _profiles.GetValue(settings, static settings => new Profile(settings));

	public static bool TryGetProfile(StorageSettings? settings, out Profile profile) {
		if (settings is not null && _profiles.TryGetValue(settings, out profile!))
			return true;
		profile = null!;
		return false;
	}

	public static void SetProfile(StorageSettings settings, Profile? profile) {
		_profiles.Remove(settings);
		if (profile is null || !profile.HasData)
			return;
		profile.Bind(settings);
		_profiles.Add(settings, profile);
	}

	public static void CopyProfile(StorageSettings target, StorageSettings source) {
		if (TryGetProfile(source, out var profile) && profile.HasData)
			SetProfile(target, profile.CloneFor(target));
		else
			SetProfile(target, null);
	}
}