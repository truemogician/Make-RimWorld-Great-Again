using System;
using System.Globalization;
using System.IO;
using System.Text;
using RimWorld;
using Verse;

namespace TrueMogician.RimWorld.ExactStorage.SSS;

using static AmountUtility;

internal sealed class ProfileFile(FileInfo file) {
	private const string _PREFIX = "ExactStorage";

	private const string _VERSION = "1";

	private const string _THING = "Thing";

	private const string _CATEGORY = "Category";

	private readonly StringBuilder _sb = new();

	public void Append(StorageSettings settings) {
		if (!Manager.TryGetProfile(settings, out var profile) || !profile.Enabled)
			return;
		try {
			_sb.Clear();
			Write("Version", _VERSION);
			Write("Enabled", profile.Enabled.ToString());
			Write("UseStackUnit", profile.UseStackUnit.ToString());
			Write("SeparateLinkedStorages", profile.SeparateLinkedStorages.ToString());
			foreach (var quota in profile.Quotas) {
				if (!quota.Valid || !quota.Active)
					continue;
				if (quota is ThingQuota { ThingDef: { } thingDef })
					WriteQuota(_THING, thingDef.defName, quota);
				else if (quota is ThingCategoryQuota { CategoryDef: { } categoryDef })
					WriteQuota(_CATEGORY, categoryDef.defName, quota);
			}
			File.AppendAllText(file.FullName, _sb.ToString());
		}
		catch (Exception e) {
			Helper.Logger.Warning($"Failed to append Exact Storage profile to '{file.Name}': {e.GetType().Name} {e.Message}");
		}
	}

	public void Load(StorageSettings settings) {
		if (!file.Exists)
			return;
		try {
			Profile? profile = null;
			foreach (string? line in File.ReadLines(file.FullName)) {
				if (!line.StartsWith(_PREFIX + "|"))
					continue;
				string[]? parts = line.Split('|');
				if (parts.Length < 3)
					continue;
				profile ??= new Profile(settings);
				if (parts[1] == "Version" && parts[2] != _VERSION)
					return;
				ReadLine(profile, parts);
			}
			if (profile is null)
				return;
			Manager.SetProfile(settings, profile);
			settings.NotifyChanged();
		}
		catch (Exception e) {
			Helper.Logger.Warning($"Failed to load Exact Storage profile from '{file.Name}': {e.GetType().Name} {e.Message}");
		}
	}

	private static void ReadLine(Profile profile, string[] parts) {
		switch (parts[1]) {
			case "Enabled":
				if (bool.TryParse(parts[2], out bool enabled))
					profile.Enabled = enabled;
				return;
			case "UseStackUnit":
				if (bool.TryParse(parts[2], out bool useStackUnit))
					profile.UseStackUnit = useStackUnit;
				return;
			case "SeparateLinkedStorages":
				if (bool.TryParse(parts[2], out bool separateLinkedStorages))
					profile.SeparateLinkedStorages = separateLinkedStorages;
				return;
			case "Quota":
				ReadQuota(profile, parts);
				return;
		}
	}

	private static void ReadQuota(Profile profile, string[] parts) {
		if (parts.Length < 6)
			return;
		Quota? quota = parts[2] switch {
			_THING => DefDatabase<ThingDef>.GetNamedSilentFail(parts[3]) is { } thingDef
				? profile.GetOrCreateQuota(thingDef)
				: null,
			_CATEGORY => DefDatabase<ThingCategoryDef>.GetNamedSilentFail(parts[3]) is { } categoryDef
				? profile.GetOrCreateQuota(categoryDef)
				: null,
			_ => null
		};
		if (quota is null)
			return;
		quota.Min = ParseStock(parts[4]);
		quota.Max = ParseStock(parts[5]);
	}

	private static decimal ParseStock(string value) =>
		decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal stock) ? Normalize(stock) : UNSET;

	private void WriteQuota(string type, string defName, Quota quota) =>
		_sb.AppendLine(string.Join('|', _PREFIX, "Quota", type, defName, Format(quota.Min), Format(quota.Max)));

	private void Write(string key, string value) =>
		_sb.AppendLine(string.Join('|', _PREFIX, key, value));
}