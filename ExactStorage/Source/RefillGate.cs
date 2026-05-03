using System;
using System.Collections.Generic;
using RimWorld;

namespace TrueMogician.RimWorld.ExactStorage;

public static class RefillGate {
	private static readonly List<Func<StorageSettings, bool>> _checks = [];

	public static void Add(Func<StorageSettings, bool> check) => _checks.Add(check);

	public static bool AllowsRefill(StorageSettings settings) {
		foreach (var check in _checks) {
			if (!check(settings))
				return false;
		}
		return true;
	}
}