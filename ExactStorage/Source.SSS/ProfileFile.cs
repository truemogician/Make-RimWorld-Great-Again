using System;
using System.Globalization;
using System.IO;
using System.Text;
using RimWorld;
using Verse;

namespace TrueMogician.RimWorld.ExactStorage.SSS;

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
			Write("UseStockUnits", profile.UseStockUnits.ToString());
			Write("SeparateLinkedStorages", profile.SeparateLinkedStorages.ToString());
			foreach (var quota in profile.Quotas) {
				if (!quota.Active || !quota.IsValidKey)
					continue;
				if (quota.ThingDef is { } thingDef)
					WriteQuota(_THING, thingDef.defName, quota);
				else if (quota.CategoryDef is { } categoryDef)
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
			foreach (var line in File.ReadLines(file.FullName)) {
				if (!line.StartsWith(_PREFIX + "|"))
					continue;
				var parts = line.Split('|');
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
			StorageUtility.NotifyChanged(settings);
		}
		catch (Exception e) {
			Helper.Logger.Warning($"Failed to load Exact Storage profile from '{file.Name}': {e.GetType().Name} {e.Message}");
		}
	}

	private static void ReadLine(Profile profile, string[] parts) {
		switch (parts[1]) {
			case "Enabled":
				if (bool.TryParse(parts[2], out var enabled))
					profile.Enabled = enabled;
				return;
			case "UseStockUnits":
				if (bool.TryParse(parts[2], out var useStockUnits))
					profile.UseStockUnits = useStockUnits;
				return;
			case "SeparateLinkedStorages":
				if (bool.TryParse(parts[2], out var separateLinkedStorages))
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
		var quota = parts[2] switch {
			_THING => DefDatabase<ThingDef>.GetNamedSilentFail(parts[3]) is { } thingDef
				? profile.GetQuota(thingDef, true)
				: null,
			_CATEGORY => DefDatabase<ThingCategoryDef>.GetNamedSilentFail(parts[3]) is { } categoryDef
				? profile.GetQuota(categoryDef, true)
				: null,
			_ => null
		};
		if (quota is null)
			return;
		quota.MinStock = ParseStock(parts[4]);
		quota.MaxStock = ParseStock(parts[5]);
	}

	private void WriteQuota(string type, string defName, Quota quota) {
		_sb.AppendLine(string.Join('|', _PREFIX, "Quota", type, defName, AmountUtility.Format(quota.MinStock), AmountUtility.Format(quota.MaxStock)));
	}

	private static decimal ParseStock(string value) =>
		decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var stock)
			? AmountUtility.Normalize(stock)
			: AmountUtility.UNSET;

	private void Write(string key, string value) {
		_sb.AppendLine(string.Join('|', _PREFIX, key, value));
	}
}
