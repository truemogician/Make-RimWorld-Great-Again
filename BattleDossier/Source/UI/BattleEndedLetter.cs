using System.Collections.Generic;
using TrueMogician.RimWorld.BattleDossier.Components;
using TrueMogician.RimWorld.BattleDossier.Models;
using Verse;

namespace TrueMogician.RimWorld.BattleDossier.UI;

/// <summary>
///     Battle-end notification, also the History → Messages archive entry. Re-opening it leads back to
///     the dossier; tolerates its record being deleted by falling back to the browser.
/// </summary>
public class BattleEndedLetter : ChoiceLetter {
	public BattleDossierRecord? Record;

	public override IEnumerable<DiaOption> Choices {
		get {
			yield return new DiaOption("BattleDossier.Letter.ViewDossier".Translate()) {
				action = OpenDossier,
				resolveTree = true
			};
			yield return Option_Close;
		}
	}

	public override void OpenLetter() => OpenDossier();

	public override void ExposeData() {
		base.ExposeData();
		Scribe_References.Look(ref Record, "record");
	}

	private void OpenDossier() {
		bool valid = Record != null && DossierManager.Instance?.Records.Contains(Record) == true;
		BattleDossierWindow.Open(valid ? Record : null);
		// Dismiss from the letter stack; it remains in the History archive.
		Find.LetterStack.RemoveLetter(this);
	}
}