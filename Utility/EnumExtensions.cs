using System;
using System.Reflection;
using Verse;

namespace TrueMogician.RimWorld.Utility;

public static class EnumExtensions {
	extension<T>(T self) where T : struct, Enum {
		public string? Name => Enum.GetName(typeof(T), self);

		public bool IsDefined => self.Name is not null;

		public FieldInfo? FieldInfo => self.Name is { } name ? typeof(T).GetField(name) : null;

		public string? Description => self.FieldInfo is not { } @field
			? null
			: @field.GetCustomAttribute<DescriptionAttribute>()?.description ??
			@field.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description;

		public byte BitFlagCount {
			get {
				byte count = 0;
				ulong value = Convert.ToUInt64(self);
				while (value != 0) {
					count += (byte)(value & 1);
					value >>= 1;
				}
				return count;
			}
		}

		public bool IsSingleBitFlag => self.BitFlagCount == 1;

		public int LsbIndex {
			get {
				ulong value = Convert.ToUInt64(self);
				if (value == 0)
					throw new InvalidOperationException("Cannot get LSB index of zero value.");
				int index = 0;
				while ((value & 1) == 0) {
					value >>= 1;
					index++;
				}
				return index;
			}
		}

		public int MsbIndex {
			get {
				ulong value = Convert.ToUInt64(self);
				if (value == 0)
					throw new InvalidOperationException("Cannot get MSB index of zero value.");
				int index = 0;
				while (value != 0) {
					value >>= 1;
					index++;
				}
				return index - 1;
			}
		}
	}
}