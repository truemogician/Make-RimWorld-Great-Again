using System.Threading.Tasks;
using Verse;

namespace TrueMogician.RimWorld.Utility.GUI;

public static class UtilityWindows {
	public static Task<bool> Confirm(
		TaggedString text,
		string? title = null,
		string trueButtonText = "Yes",
		string falseButtonText = "No",
		WindowLayer layer = WindowLayer.Dialog
	) {
		var tcs = new TaskCompletionSource<bool>();
		Find.WindowStack.Add(
			new Dialog_MessageBox(
				text,
				trueButtonText,
				() => tcs.SetResult(true),
				falseButtonText,
				() => tcs.SetResult(false),
				title,
				layer: layer
			)
		);
		return tcs.Task;
	}
}