using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;

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
	extension(Harmony harmony) {
		public void PatchFromType(Type patchType) {
			List<MethodInfo> beforeHooks = [], afterHooks = [];
			foreach (var method in patchType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)) {
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
				InvokeHook(beforeHook, PatchHookTiming.BeforePatch);
			harmony.CreateClassProcessor(patchType).Patch();
			foreach (var afterHook in afterHooks)
				InvokeHook(afterHook, PatchHookTiming.AfterPatch);
		}

		public void UnpatchFromType(Type patchType) {
			List<MethodInfo> beforeHooks = [], afterHooks = [];
			foreach (var method in patchType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)) {
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
				InvokeHook(beforeHook, PatchHookTiming.BeforeUnpatch);
			harmony.CreateClassProcessor(patchType).Unpatch();
			foreach (var afterHook in afterHooks)
				InvokeHook(afterHook, PatchHookTiming.AfterUnpatch);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void InvokeHook(MethodInfo method, PatchHookTiming timing) {
		method.Invoke(null, method.GetParameters().Length == 0 ? null : [timing]);
	}

	private static void ValidateHookMethod(MethodInfo method) {
		if (!method.IsStatic)
			throw new ArgumentException($"Patch hook method {method.Name} must be static.");
		var @params = method.GetParameters();
		if (@params.Length > 1 || (@params.Length == 1 && @params[0].ParameterType != typeof(PatchHookTiming)))
			throw new ArgumentException($"Patch hook method {method.Name} must have zero or one parameter of type {nameof(PatchHookTiming)}.");
	}
}