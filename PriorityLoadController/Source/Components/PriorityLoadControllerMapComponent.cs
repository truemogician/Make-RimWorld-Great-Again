using System.Collections.Generic;
using RimWorld;
using Verse;

namespace TrueMogician.RimWorld.PriorityLoadController.Components;

public sealed class PriorityLoadControllerMapComponent(Map map) : MapComponent(map) {
	private readonly HashSet<CompPriorityLoadController> _controllers = [];

	public bool AnyControllers => _controllers.Count > 0;

	public void Register(CompPriorityLoadController controller) => _controllers.Add(controller);

	public void Deregister(CompPriorityLoadController controller) => _controllers.Remove(controller);

	public IEnumerable<CompPriorityLoadController> ActiveControllersFor(PowerNet net) {
		foreach (var controller in _controllers) {
			if (controller.IsActive && ReferenceEquals(controller.PowerNet, net))
				yield return controller;
		}
	}

	public bool HasActiveControllerFor(PowerNet net) {
		foreach (var controller in _controllers) {
			if (controller.IsActive && ReferenceEquals(controller.PowerNet, net))
				return true;
		}
		return false;
	}

	public StoragePriority GetEffectivePriority(PowerNet net, CompPowerTrader trader) {
		var max = StoragePriority.Normal;
		foreach (var controller in _controllers) {
			if (!controller.IsActive || !ReferenceEquals(controller.PowerNet, net))
				continue;
			var priority = controller.GetPriority(trader);
			if (priority > max)
				max = priority;
		}
		return max;
	}
}