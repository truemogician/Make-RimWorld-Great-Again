using System;
using Verse;

namespace TrueMogician.RimWorld.Utility;

public class CorruptedDataException : Exception {
	public CorruptedDataException() { }

	public CorruptedDataException(Type type) : base($"Corrupted save/settings data when loading type {type.AssemblyQualifiedName}") => Type = type;

	public CorruptedDataException(string message, Type? type = null) : base(message) => Type = type;

	public Type? Type { get; }
}

public class InvalidPawnException(string message, Pawn pawn) : Exception(message) {
	public InvalidPawnException(Pawn pawn) : this($"Invalid pawn: {pawn.Name}", pawn) { }

	public Pawn Pawn { get; } = pawn;
}