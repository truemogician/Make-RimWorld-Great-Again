using RimWorld;
using UnityEngine;

namespace TrueMogician.RimWorld.Profiler.Windows;

public sealed class TickProfilerMainTabWindow : MainTabWindow {
	private readonly TickProfilerReportWindow _view = new();

	public TickProfilerMainTabWindow() {
		doCloseButton = false;
		doCloseX = false;
		draggable = false;
		absorbInputAroundWindow = false;
		closeOnAccept = false;
		closeOnClickedOutside = false;
	}

	public override void DoWindowContents(Rect inRect) {
		_view.DoManagerContents(inRect, false);
	}
}