using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace TrueMogician.RimWorld.Utility;

public abstract class ClosureFinder {
	private readonly List<MethodBase> _closures = [];

	public IReadOnlyList<MethodBase> Closures => _closures;

	public IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> insts) {
		_closures.Clear();
		HashSet<MethodBase> uniqueClosures = [];
		var list = new List<CodeInstruction>();
		foreach (var inst in insts) {
			list.Add(inst);
			yield return inst;
		}
		foreach (var closure in FindClosures(list)) {
			if (uniqueClosures.Add(closure))
				_closures.Add(closure);
		}
	}

	public abstract IEnumerable<MethodBase> FindClosures(IReadOnlyList<CodeInstruction> insts);
}

public class AssignmentClosureFinder : ClosureFinder {
	private readonly Predicate<MemberInfo> _predicate;

	public AssignmentClosureFinder(MemberInfo target) => _predicate = member => member == target;

	public AssignmentClosureFinder(Predicate<MemberInfo> predicate) => _predicate = predicate;

	public AssignmentClosureFinder(Type targetType, string targetName) {
		if (AccessTools.Field(targetType, targetName) is { } field)
			_predicate = member => member == field;
		else if (AccessTools.Property(targetType, targetName) is { } property)
			_predicate = member => member == property;
		else
			throw new ArgumentException($"No field or property named {targetName} found in {targetType}");
	}

	public override IEnumerable<MethodBase> FindClosures(IReadOnlyList<CodeInstruction> insts) {
		for (var i = 0; i < insts.Count; i++) {
			if (!TryGetAssignedMember(insts[i], out var member) || !_predicate(member))
				continue;
			if (ClosureFinderUtility.TryExtractClosure(insts, i - 1, out var closure))
				yield return closure;
		}
	}

	private static bool TryGetAssignedMember(CodeInstruction inst, out MemberInfo member) {
		if (inst.operand is FieldInfo field && inst.StoresField(field)) {
			member = field;
			return true;
		}
		if (inst.operand is MethodInfo method && TryGetPropertyFromSetter(method, out var property)) {
			member = property;
			return true;
		}
		member = null!;
		return false;
	}

	private static bool TryGetPropertyFromSetter(MethodInfo method, out PropertyInfo property) {
		property = null!;
		if (!method.IsSpecialName || !method.Name.StartsWith("set_", StringComparison.Ordinal) || method.DeclaringType is null)
			return false;
		var propertyName = method.Name[4..];
		property = AccessTools.Property(method.DeclaringType, propertyName);
		return property?.GetSetMethod(true) == method;
	}
}

public class ParameterClosureFinder : ClosureFinder {
	private readonly MethodInfo _method;

	private readonly int _paramIndex;

	public ParameterClosureFinder(MethodInfo method, int paramIndex) {
		_method = method;
		_paramIndex = paramIndex;
	}

	public ParameterClosureFinder(MethodInfo method, string paramName) {
		_method = method;
		_paramIndex = ResolveParameterIndex(method, paramName);
	}

	public ParameterClosureFinder(Type targetType, string methodName, int paramIndex) {
		if (AccessTools.Method(targetType, methodName) is { } method)
			_method = method;
		else
			throw new ArgumentException($"No method named {methodName} found in {targetType}");
		_paramIndex = paramIndex;
	}

	public ParameterClosureFinder(Type targetType, string methodName, string paramName) {
		if (AccessTools.Method(targetType, methodName) is { } method)
			_method = method;
		else
			throw new ArgumentException($"No method named {methodName} found in {targetType}");
		_paramIndex = ResolveParameterIndex(_method, paramName);
	}

	public override IEnumerable<MethodBase> FindClosures(IReadOnlyList<CodeInstruction> insts) {
		var parameters = _method.GetParameters();
		if (_paramIndex < 0 || _paramIndex >= parameters.Length)
			throw new ArgumentOutOfRangeException(nameof(_paramIndex), $"Parameter index {_paramIndex} is out of range for {_method}.");
		for (var i = 0; i < insts.Count; i++) {
			if (!insts[i].Calls(_method))
				continue;
			int cursor = ClosureFinderUtility.FindPreviousNonNopInstruction(insts, i - 1);
			for (var paramIndex = parameters.Length - 1; paramIndex >= 0 && cursor >= 0; paramIndex--) {
				if (!ClosureFinderUtility.TryFindExpressionStart(insts, cursor, out int startIndex))
					break;
				if (paramIndex == _paramIndex) {
					if (ClosureFinderUtility.TryExtractClosure(insts, cursor, startIndex, out var closure))
						yield return closure;
					break;
				}
				cursor = ClosureFinderUtility.FindPreviousNonNopInstruction(insts, startIndex - 1);
			}
		}
	}

	private static int ResolveParameterIndex(MethodInfo method, string paramName) {
		if (string.IsNullOrEmpty(paramName))
			throw new ArgumentException("Parameter name cannot be null or empty.", nameof(paramName));
		var parameters = method.GetParameters();
		for (var i = 0; i < parameters.Length; i++) {
			if (parameters[i].Name == paramName)
				return i;
		}
		throw new ArgumentException($"No parameter named {paramName} found in {method}.");
	}
}

internal static class ClosureFinderUtility {
	public static bool TryExtractClosure(IReadOnlyList<CodeInstruction> insts, int endIndex, out MethodBase closure) =>
		TryExtractClosure(insts, endIndex, 0, out closure);

	public static bool TryExtractClosure(IReadOnlyList<CodeInstruction> insts, int endIndex, int lowerBound, out MethodBase closure) {
		closure = null!;
		int ctorIndex = FindPreviousNonNopInstruction(insts, endIndex, lowerBound);
		if (ctorIndex < lowerBound
			|| insts[ctorIndex].opcode != OpCodes.Newobj
			|| insts[ctorIndex].operand is not ConstructorInfo ctor
			|| !IsDelegateConstructor(ctor))
			return false;
		int ldftnIndex = FindPreviousNonNopInstruction(insts, ctorIndex - 1, lowerBound);
		if (ldftnIndex < lowerBound)
			return false;
		var ldftn = insts[ldftnIndex];
		if (ldftn.opcode != OpCodes.Ldftn && ldftn.opcode != OpCodes.Ldvirtftn)
			return false;
		if (ldftn.operand is not MethodBase method)
			return false;
		closure = method;
		return true;
	}

	public static int FindPreviousNonNopInstruction(IReadOnlyList<CodeInstruction> insts, int startIndex, int lowerBound = 0) {
		for (var i = startIndex; i >= lowerBound; i--) {
			if (insts[i].opcode != OpCodes.Nop)
				return i;
		}
		return lowerBound - 1;
	}

	public static bool TryFindExpressionStart(IReadOnlyList<CodeInstruction> insts, int endIndex, out int startIndex) {
		var neededValues = 1;
		for (var i = endIndex; i >= 0; i--) {
			var inst = insts[i];
			if (inst.opcode == OpCodes.Nop)
				continue;
			neededValues += GetPopCount(inst) - GetPushCount(inst);
			if (neededValues <= 0) {
				startIndex = i;
				return true;
			}
		}
		startIndex = -1;
		return false;
	}

	private static bool IsDelegateConstructor(ConstructorInfo ctor) => typeof(Delegate).IsAssignableFrom(ctor.DeclaringType);

	private static int GetPopCount(CodeInstruction inst) => inst.opcode.StackBehaviourPop switch {
		StackBehaviour.Pop0                                                 => 0,
		StackBehaviour.Pop1 or StackBehaviour.Popi or StackBehaviour.Popref => 1,
		StackBehaviour.Pop1_pop1
			or StackBehaviour.Popi_pop1
			or StackBehaviour.Popi_popi
			or StackBehaviour.Popi_popi8
			or StackBehaviour.Popi_popr4
			or StackBehaviour.Popi_popr8
			or StackBehaviour.Popref_pop1
			or StackBehaviour.Popref_popi => 2,
		StackBehaviour.Popi_popi_popi
			or StackBehaviour.Popref_popi_popi
			or StackBehaviour.Popref_popi_popi8
			or StackBehaviour.Popref_popi_popr4
			or StackBehaviour.Popref_popi_popr8
			or StackBehaviour.Popref_popi_popref => 3,
		StackBehaviour.Varpop => GetVariablePopCount(inst),
		_                     => throw new NotSupportedException($"Unsupported pop behavior {inst.opcode.StackBehaviourPop} for {inst}")
	};

	private static int GetPushCount(CodeInstruction inst) => inst.opcode.StackBehaviourPush switch {
		StackBehaviour.Push0 => 0,
		StackBehaviour.Push1
			or StackBehaviour.Pushi
			or StackBehaviour.Pushi8
			or StackBehaviour.Pushr4
			or StackBehaviour.Pushr8
			or StackBehaviour.Pushref => 1,
		StackBehaviour.Push1_push1 => 2,
		StackBehaviour.Varpush     => GetVariablePushCount(inst),
		_                          => throw new NotSupportedException($"Unsupported push behavior {inst.opcode.StackBehaviourPush} for {inst}")
	};

	private static int GetVariablePopCount(CodeInstruction inst) {
		if (inst.opcode == OpCodes.Newobj && inst.operand is ConstructorInfo ctor)
			return ctor.GetParameters().Length;
		if ((inst.opcode == OpCodes.Call || inst.opcode == OpCodes.Callvirt) && inst.operand is MethodInfo method)
			return method.GetParameters().Length + (method.IsStatic ? 0 : 1);
		throw new NotSupportedException($"Unsupported variable pop instruction {inst}");
	}

	private static int GetVariablePushCount(CodeInstruction inst) {
		if (inst.opcode == OpCodes.Newobj)
			return 1;
		if ((inst.opcode == OpCodes.Call || inst.opcode == OpCodes.Callvirt) && inst.operand is MethodInfo method)
			return method.ReturnType == typeof(void) ? 0 : 1;
		throw new NotSupportedException($"Unsupported variable push instruction {inst}");
	}
}