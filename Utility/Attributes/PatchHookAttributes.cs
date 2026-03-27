using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Verse;

namespace TrueMogician.RimWorld.Utility.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class PatchHookAttribute(PatchHookTiming timing) : Attribute {
	public PatchHookTiming Timing { get; } = timing;
}

public enum PatchHookTiming : byte {
	BeforePatch,
	AfterPatch,
	BeforeUnpatch,
	AfterUnpatch
}

public static class PatchHookHelper {
	private static readonly Type[] _hookParamTypes = [typeof(PatchHookTiming), typeof(Harmony)];

	extension(Harmony harmony) {
		public void PatchFromType(Type patchType) {
			List<MethodInfo> beforeHooks = [], afterHooks = [];
			foreach (var method in patchType.GetMethods(AccessTools.allDeclared)) {
				var attrs = method.GetCustomAttributes<PatchHookAttribute>().ToArray();
				if (attrs.Length == 0)
					continue;
				ValidateHookMethod(method);
				if (attrs.Any(a => a.Timing == PatchHookTiming.BeforePatch))
					beforeHooks.Add(method);
				if (attrs.Any(a => a.Timing == PatchHookTiming.AfterPatch))
					afterHooks.Add(method);
			}
			foreach (var beforeHook in beforeHooks)
				InvokeHook(beforeHook, PatchHookTiming.BeforePatch, harmony);
			harmony.CreateClassProcessor(patchType).Patch();
			foreach (var afterHook in afterHooks)
				InvokeHook(afterHook, PatchHookTiming.AfterPatch, harmony);
		}

		public void UnpatchFromType(Type patchType) {
			List<MethodInfo> beforeHooks = [], afterHooks = [];
			foreach (var method in patchType.GetMethods(AccessTools.allDeclared)) {
				var attrs = method.GetCustomAttributes<PatchHookAttribute>().ToArray();
				if (attrs.Length == 0)
					continue;
				ValidateHookMethod(method);
				if (attrs.Any(a => a.Timing == PatchHookTiming.BeforeUnpatch))
					beforeHooks.Add(method);
				if (attrs.Any(a => a.Timing == PatchHookTiming.AfterUnpatch))
					afterHooks.Add(method);
			}
			foreach (var beforeHook in beforeHooks)
				InvokeHook(beforeHook, PatchHookTiming.BeforeUnpatch, harmony);
			harmony.CreateClassProcessor(patchType).Unpatch();
			foreach (var afterHook in afterHooks)
				InvokeHook(afterHook, PatchHookTiming.AfterUnpatch, harmony);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void InvokeHook(MethodInfo method, PatchHookTiming timing, Harmony harmony) {
		var @params = method.GetParameters();
		if (@params.Length == 0)
			method.Invoke(null, null);
		else {
			var values = new object[@params.Length];
			var idx = @params.FirstIndexOf(p => p.ParameterType == typeof(PatchHookTiming));
			if (idx != -1)
				values[idx] = timing;
			idx = @params.FirstIndexOf(p => p.ParameterType == typeof(Harmony));
			if (idx != -1)
				values[idx] = harmony;
			if (values.Any(v => v == null))
				throw new ArgumentException($"Could not resolve parameters for patch hook method {method.Name}.");
			method.Invoke(null, values);
		}
	}

	private static void ValidateHookMethod(MethodInfo method) {
		if (!method.IsStatic)
			throw new ArgumentException($"Patch hook method {method.Name} must be static.");
		var @params = method.GetParameters();
		if (@params.Length > 2)
			throw new ArgumentException($"Patch hook method {method.Name} cannot have more than two parameters.");
		if (@params.Any(p => !_hookParamTypes.Contains(p.ParameterType)))
			throw new ArgumentException($"Patch hook method {method.Name} has invalid parameter types. Allowed types are: {string.Join(", ", _hookParamTypes.Select(t => t.Name))}");
	}
}