using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using TrueMogician.RimWorld.Rimfined.Components;
using TrueMogician.RimWorld.Utility.Extensions;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace TrueMogician.RimWorld.Rimfined.Patches;

using static DelayedQuestAcceptanceUtility;

internal static class DelayedQuestAcceptancePatches {
	private const float _ROW_HEIGHT = 40f;

	private const float _ROW_GAP = 4f;

	private const float _CONTROL_GAP = 6f;

	private const float _TOP_ICON_SIZE = 24f;

	private const float _TOP_ICON_GAP = 6f;

	private static readonly Color _scheduledTimeColor = new(0.45f, 0.82f, 1f);

	private static readonly Color _timeInfoColor = new(1f, 1f, 1f, 0.7f);

	private static readonly Color _scheduledChoiceBackgroundColor = new(0.18f, 0.24f, 0.34f);

	private static readonly Color _scheduledChoiceOutlineColor = new(0.43f, 0.65f, 0.95f);

	private static readonly Texture2D _cancelScheduleIcon = ContentFinder<Texture2D>.Get("UI/Buttons/CancelQuestAcceptanceSchedule");

	private static readonly Texture2D _acceptNowIcon = ContentFinder<Texture2D>.Get("UI/Buttons/AcceptQuestNow");

	private static readonly Texture2D _charityQuestIcon = ContentFinder<Texture2D>.Get("UI/Icons/CharityQuestIcon");

	private static readonly FieldInfo _selectedField = AccessTools.Field(typeof(MainTabWindow_Quests), "selected")
		?? throw new MissingFieldException(typeof(MainTabWindow_Quests).FullName, "selected");

	private static readonly MethodInfo _acceptQuestByInterfaceMethod = AccessTools.Method(typeof(MainTabWindow_Quests), "AcceptQuestByInterface")
		?? throw new MissingMethodException(typeof(MainTabWindow_Quests).FullName, "AcceptQuestByInterface");

	[HarmonyPatch(typeof(MainTabWindow_Quests), "DoAcceptButton")]
	[HarmonyPrefix]
	internal static bool MainTabWindow_Quests_DoAcceptButton_Prefix(MainTabWindow_Quests __instance, Rect innerRect, ref float curY) {
		if (GetSelectedQuest(__instance) is not { } quest)
			return false;
		var choicePart = GetChoicePart(quest);
		if (choicePart is not null && !Prefs.DevMode)
			return false;
		float baseY = curY + 17f;
		if (quest.State != QuestState.NotYetAccepted) {
			curY = baseY;
			return false;
		}
		bool scheduled = Manager.TryGetSchedule(quest, out _);
		bool drawMainControls = choicePart is null && !scheduled;
		float y = baseY;
		if (drawMainControls) {
			DrawAcceptButtonRow(__instance, quest, new Rect(innerRect.x, y, innerRect.width, _ROW_HEIGHT));
			y += _ROW_HEIGHT + _ROW_GAP;
			DrawControlStrip(quest, new Rect(innerRect.x, y, innerRect.width, _ROW_HEIGHT));
			y += _ROW_HEIGHT + _ROW_GAP;
		}
		if (Prefs.DevMode) {
			DrawDevAcceptButton(quest, new Rect(innerRect.x, y, 180f, _ROW_HEIGHT));
			y += _ROW_HEIGHT + _ROW_GAP;
		}
		if (drawMainControls || Prefs.DevMode)
			curY = y;
		return false;
	}

	[HarmonyPatch(typeof(MainTabWindow_Quests), "DoRewards")]
	[HarmonyPrefix]
	internal static bool MainTabWindow_Quests_DoRewards_Prefix(MainTabWindow_Quests __instance, Rect innerRect, ref float curY) {
		if (GetSelectedQuest(__instance) is not { } quest)
			return false;
		var choicePart = GetChoicePart(quest);
		if (choicePart is null)
			return false;
		var stackElements = new List<GenUI.AnonymousStackElement>();
		bool scheduled = Manager.TryGetSchedule(quest, out var schedule);
		bool showAcceptButtons = quest.State == QuestState.NotYetAccepted && !scheduled;

		for (var j = 0; j < choicePart.choices.Count; j++) {
			stackElements.Clear();
			var totalValue = 0f;
			foreach (var reward in choicePart.choices[j].rewards) {
				stackElements.AddRange(reward.StackElements);
				totalValue += reward.TotalMarketValue;
			}
			if (!stackElements.Any())
				continue;
			if (totalValue > 0f
				&& (
					choicePart.choices[j].rewards.Count != 1
					|| choicePart.choices[j].rewards[0] is not Reward_Items { items: not null } rewardItems
					|| rewardItems.items.Count != 1
					|| rewardItems.items[0].StyleSourcePrecept is not Precept_Relic
				)) {
				string totalValueStr = "TotalValue".Translate(totalValue.ToStringMoney("F0"));
				stackElements.Add(
					new GenUI.AnonymousStackElement {
						drawer = rect => {
							GUI.color = new Color(0.7f, 0.7f, 0.7f);
							Widgets.Label(new Rect(rect.x + 5f, rect.y, rect.width - 10f, rect.height), totalValueStr);
							GUI.color = Color.white;
						},
						width = Text.CalcSize(totalValueStr).x + 10f
					}
				);
			}

			curY += j == 0 ? 17f : 10f;
			var rect = new Rect(innerRect.x, curY, innerRect.width, 10000f);
			var contentsRect = rect.ContractedBy(10f);
			string actionLabel = showAcceptButtons ? GetRewardActionLabel(quest) : string.Empty;
			float actionWidth = showAcceptButtons ? GetRewardActionWidth(actionLabel) : 0f;
			if (showAcceptButtons)
				contentsRect.xMin += actionWidth;
			rect.height = GenUI.DrawElementStack(contentsRect, 24f, stackElements, null, obj => obj.width, 4f, 5f, false).height + 20f;
			bool selectedChoice = schedule is { ChoiceIndex: var idx } && idx == j;
			if (selectedChoice)
				Widgets.DrawBoxSolidWithOutline(rect, _scheduledChoiceBackgroundColor, _scheduledChoiceOutlineColor);
			else
				Widgets.DrawBoxSolid(rect, new Color(0.13f, 0.13f, 0.13f));
			GUI.color = new Color(1f, 1f, 1f, 0.3f);
			Widgets.DrawHighlightIfMouseover(rect);
			GUI.color = Color.white;
			var drawRect = rect.ContractedBy(10f);
			if (showAcceptButtons) {
				drawRect.x += actionWidth;
				drawRect.width -= actionWidth;
			}
			GenUI.DrawElementStack(drawRect, 24f, stackElements, (r, obj) => obj.drawer(r), obj => obj.width, 4f, 5f, false);

			if (showAcceptButtons)
				DrawAcceptOrScheduleButton(__instance, quest, j, new Rect(rect.x, rect.y, actionWidth, rect.height), actionLabel, true);

			curY += rect.height;
		}

		if (showAcceptButtons) {
			curY += 10f;
			DrawControlStrip(quest, new Rect(innerRect.x, curY, innerRect.width, _ROW_HEIGHT));
			curY += _ROW_HEIGHT + _ROW_GAP;
		}
		return false;
	}

	[HarmonyPatch(typeof(MainTabWindow_Quests), "DoDismissButton")]
	[HarmonyPostfix]
	internal static void MainTabWindow_Quests_DoDismissButton_Postfix(MainTabWindow_Quests __instance, Rect innerRect) {
		if (GetSelectedQuest(__instance) is not { } quest)
			return;
		if (!Manager.TryGetSchedule(quest, out var schedule))
			return;
		GetScheduledActionRects(innerRect, out var cancelRect, out var acceptNowRect);
		if (Widgets.ButtonImage(cancelRect, _cancelScheduleIcon, true, Translate("Buttons.Cancel"))) {
			if (Manager.CancelSchedule(quest))
				Messages.Message(Translate("Messages.ScheduledCanceled"), MessageTypeDefOf.TaskCompletion, false);
		}
		if (Widgets.ButtonImage(acceptNowRect, _acceptNowIcon, true, Translate("Buttons.AcceptNow")))
			AcceptNow(__instance, quest, schedule.ChoiceIndex >= 0 ? schedule.ChoiceIndex : null, schedule.Accepter);
	}

	[HarmonyPatch(typeof(MainTabWindow_Quests), "DoCharityIcon")]
	[HarmonyPrefix]
	internal static bool MainTabWindow_Quests_DoCharityIcon_Prefix(MainTabWindow_Quests __instance, Rect innerRect) {
		if (GetSelectedQuest(__instance) is not { charity: true } quest || !ModsConfig.IdeologyActive)
			return false;
		if (!Manager.TryGetSchedule(quest, out _))
			return true;
		var rect = GetCharityIconRect(innerRect, true);
		GUI.DrawTexture(rect, _charityQuestIcon);
		if (Mouse.IsOver(rect))
			TooltipHandler.TipRegion(rect, "CharityQuestTip".Translate());
		return false;
	}

	[HarmonyPatch(typeof(MainTabWindow_Quests), "DoRightAlignedInfo")]
	[HarmonyPrefix]
	internal static bool MainTabWindow_Quests_DoRightAlignedInfo_Prefix(
		MainTabWindow_Quests __instance,
		Rect innerRect,
		ref float curY,
		float curYBeforeAcceptButton
	) {
		if (GetSelectedQuest(__instance) is not { } quest)
			return true;
		if (!Manager.TryGetSchedule(quest, out var schedule))
			return true;

		float num = curYBeforeAcceptButton + 17f;
		var rect = new Rect(innerRect.x, num, innerRect.width, 25f);
		string countdown = GetCountdownLabel(schedule.FireTick);
		string text = countdown;
		string tip = GetScheduledTooltip(schedule.FireTick);
		if (quest is { State: QuestState.NotYetAccepted, TicksUntilExpiry: > 0 }) {
			text = $"{"QuestExpiresIn".Translate(quest.TicksUntilExpiry.ToStringTicksToPeriod())} ({countdown})";
			var expText = GenDate.DateFullStringWithHourAt(Find.TickManager.TicksAbs + quest.TicksUntilExpiry, QuestUtility.GetLocForDates());
			tip = $"{"QuestExpiresOn".Translate(expText)}\n{tip}";
		}
		num += Text.LineHeight;
		using (new TextBlock(_timeInfoColor, TextAnchor.MiddleRight, false))
			Widgets.Label(rect, text);
		rect.xMin = rect.xMax - Text.CalcSize(text).x;
		if (Mouse.IsOver(rect))
			TooltipHandler.TipRegion(rect, tip);
		curY = Mathf.Max(curY, num);
		return false;
	}

	[HarmonyPatch(typeof(MainTabWindow_Quests), "GetShortTimeInfo")]
	[HarmonyPostfix]
	internal static void MainTabWindow_Quests_GetShortTimeInfo_Postfix(Quest quest, ref string? __result, ref string? tip, ref Color color) {
		if (__result.NullOrEmpty() || quest.State != QuestState.NotYetAccepted)
			return;
		if (!Manager.TryGetSchedule(quest, out var schedule))
			return;
		color = _scheduledTimeColor;
		string scheduleTip = GetScheduledTooltip(schedule.FireTick);
		tip = tip.NullOrEmpty() ? scheduleTip : $"{tip}\n{scheduleTip}";
	}

	[HarmonyPatch(typeof(Quest), nameof(Quest.Accept))]
	[HarmonyPostfix]
	internal static void Quest_Accept_Postfix(Quest __instance) => Manager.CancelSchedule(__instance, false);

	private static Quest? GetSelectedQuest(MainTabWindow_Quests window) => _selectedField.GetValue(window) as Quest;

	private static void DrawAcceptButtonRow(MainTabWindow_Quests window, Quest quest, Rect row) {
		var draft = Manager.GetDraft(quest);
		string label = draft.Enabled ? Translate("Buttons.ScheduleAccept") : "AcceptQuest".Translate();
		float width = Mathf.Min(row.width, Mathf.Max(180f, Text.CalcSize(label).x + 32f));
		DrawAcceptOrScheduleButton(window, quest, null, new Rect(row.x, row.y, width, row.height), label, false);
	}

	private static void DrawAcceptOrScheduleButton(
		MainTabWindow_Quests window,
		Quest quest,
		int? choiceIndex,
		Rect buttonRect,
		string label,
		bool rewardChoice
	) {
		var draft = Manager.GetDraft(quest);
		bool delayed = draft.Enabled;
		var acceptanceReport = QuestUtility.CanAcceptQuest(quest);
		bool validSchedule = TryGetScheduledFireTick(quest, draft, out int fireTick, out string? scheduleError);
		if ((!delayed && !acceptanceReport.Accepted) || (delayed && !validSchedule))
			GUI.color = Color.grey;
		if (Widgets.ButtonText(buttonRect, label)) {
			if (!delayed)
				AcceptNow(window, quest, choiceIndex);
			else
				ScheduleAcceptanceByInterface(quest, choiceIndex, draft);
		}
		TooltipHandler.TipRegion(buttonRect, GetActionTooltip(acceptanceReport, delayed, validSchedule, fireTick, scheduleError, rewardChoice));
		GUI.color = Color.white;
	}

	private static void DrawDevAcceptButton(Quest quest, Rect rect) {
		if (!Widgets.ButtonText(rect, "DEV: Accept instantly"))
			return;
		var choicePart = GetChoicePart(quest);
		SoundDefOf.Quest_Accepted.PlayOneShotOnCamera();
		if (choicePart?.choices.Any() == true)
			choicePart.Choose(choicePart.choices.RandomElement());
		quest.Accept(
			PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists_NoSuspended.Where(p => QuestUtility.CanPawnAcceptQuest(p, quest))
				.RandomElementWithFallback()
		);
		quest.dismissed = false;
	}

	private static void DrawControlStrip(Quest quest, Rect row) {
		var draft = Manager.GetDraft(quest);
		var rects = row.ToFlexbox([75f, "3fr", 60f, "1fr", "2.5fr"], _CONTROL_GAP).ToArray();

		DrawToggle(quest, draft, rects[0]);
		Widgets.Dropdown(
			rects[1],
			quest,
			_ => draft.Preset,
			_ => GeneratePresetOptions(quest, draft),
			GetPresetLabel(draft.Preset)
		);
		DrawAmountField(quest, draft, rects[2].Padding(6f, 0f));
		Widgets.Dropdown(
			rects[3],
			quest,
			_ => draft.IsDay,
			_ => GenerateUnitOptions(quest, draft),
			GetUnitLabel(draft.IsDay)
		);
		if (quest.acceptanceExpireTick < 0) {
			Widgets.ButtonText(
				rects[4],
				GetDirectionLabel(false),
				true,
				false,
				false
			);
		}
		else {
			Widgets.Dropdown(
				rects[4],
				quest,
				_ => draft.BeforeExpiration,
				_ => GenerateDirectionOptions(quest, draft),
				GetDirectionLabel(draft.BeforeExpiration)
			);
		}
	}

	private static void DrawToggle(Quest quest, DelayedQuestAcceptanceDraft draft, Rect rect) {
		bool enabled = draft.Enabled;
		Widgets.Checkbox(new Vector2(rect.x, rect.y + (rect.height - 24f) / 2f), ref enabled);
		if (enabled != draft.Enabled) {
			draft.Enabled = enabled;
			Manager.SetDraft(quest, draft);
		}
		var labelRect = new Rect(rect.x + 28f, rect.y, rect.width - 28f, rect.height);
		var anchor = Text.Anchor;
		Text.Anchor = TextAnchor.MiddleLeft;
		Widgets.Label(labelRect, Translate("Control.Delay"));
		Text.Anchor = anchor;
		if (Mouse.IsOver(rect))
			TooltipHandler.TipRegion(rect, Translate("Control.DelayTip"));
	}

	private static void DrawAmountField(Quest quest, DelayedQuestAcceptanceDraft draft, Rect rect) {
		int amount = draft.Amount;
		string? buffer = draft.AmountBuffer;
		Widgets.TextFieldNumeric(rect, ref amount, ref buffer, 1f, 9999f);
		if (amount == draft.Amount && buffer == draft.AmountBuffer)
			return;
		draft.Amount = amount;
		draft.AmountBuffer = buffer;
		Manager.SetDraft(quest, draft);
	}

	private static IEnumerable<Widgets.DropdownMenuElement<T>> GenerateEnumOptions<T>(Func<T, string> label, Func<T, Action?> onChoose)
		where T : struct, Enum {
		foreach (T value in Enum.GetValues(typeof(T))) {
			yield return new Widgets.DropdownMenuElement<T> {
				payload = value,
				option = new FloatMenuOption(label(value), onChoose(value))
			};
		}
	}

	private static IEnumerable<Widgets.DropdownMenuElement<DelayedQuestAcceptancePreset>> GeneratePresetOptions(
		Quest quest,
		DelayedQuestAcceptanceDraft draft
	) => GenerateEnumOptions<DelayedQuestAcceptancePreset>(
		GetPresetLabel,
		preset => quest.acceptanceExpireTick >= 0
			|| preset is not (DelayedQuestAcceptancePreset.OneDayBeforeExpiration or DelayedQuestAcceptancePreset.RightBeforeExpiration)
				? () => {
					draft.ApplyPreset(preset, quest);
					Manager.SetDraft(quest, draft);
				}
				: null
	);

	private static IEnumerable<Widgets.DropdownMenuElement<bool>> GenerateUnitOptions(Quest quest, DelayedQuestAcceptanceDraft draft) {
		foreach (bool value in new[] { false, true }) {
			yield return new Widgets.DropdownMenuElement<bool> {
				payload = value,
				option = new FloatMenuOption(
					GetUnitLabel(value),
					() => {
						draft.IsDay = value;
						Manager.SetDraft(quest, draft);
					}
				)
			};
		}
	}

	private static IEnumerable<Widgets.DropdownMenuElement<bool>> GenerateDirectionOptions(Quest quest, DelayedQuestAcceptanceDraft draft) {
		foreach (bool value in new[] { false, true }) {
			yield return new Widgets.DropdownMenuElement<bool> {
				payload = value,
				option = new FloatMenuOption(
					GetDirectionLabel(value),
					() => {
						draft.BeforeExpiration = value;
						draft.NormalizeFor(quest);
						Manager.SetDraft(quest, draft);
					}
				)
			};
		}
	}

	private static void AcceptNow(MainTabWindow_Quests window, Quest quest, int? choiceIndex, Pawn? accepter = null) {
		if ((choiceIndex is { } idx0 ? RequiresAccepter(quest, idx0) : quest.RequiresAccepter)
			&& accepter is not null
			&& QuestUtility.CanAcceptQuest(quest).Accepted
			&& QuestUtility.CanPawnAcceptQuest(accepter, quest)) {
			AcceptWithAccepter(window, quest, choiceIndex, accepter);
			return;
		}
		if (choiceIndex is not { } idx) {
			InvokeAcceptQuestByInterface(window, null, quest.RequiresAccepter);
			return;
		}
		if (!TryResolveChoice(quest, idx, out var choicePart, out var choice)) {
			Manager.CancelSchedule(quest);
			Messages.Message(Translate("Messages.CanceledInvalid", quest.name), MessageTypeDefOf.RejectInput, false);
			return;
		}
		InvokeAcceptQuestByInterface(window, () => choicePart.Choose(choice), RequiresAccepter(quest, idx));
	}

	private static void AcceptWithAccepter(MainTabWindow_Quests window, Quest quest, int? choiceIndex, Pawn accepterPawn) {
		var acceptanceReport = QuestUtility.CanAcceptQuest(quest);
		if (!acceptanceReport.Accepted) {
			Messages.Message("MessageCannotAcceptQuest".Translate(), MessageTypeDefOf.RejectInput, false);
			return;
		}
		if (!QuestUtility.CanPawnAcceptQuest(accepterPawn, quest)) {
			Messages.Message("MessageNoColonistCanAcceptQuest".Translate(Faction.OfPlayer.def.pawnsPlural), MessageTypeDefOf.RejectInput, false);
			return;
		}
		if (choiceIndex is { } idx) {
			if (!TryResolveChoice(quest, idx, out var choicePart, out var choice)) {
				Manager.CancelSchedule(quest);
				Messages.Message(Translate("Messages.CanceledInvalid", quest.name), MessageTypeDefOf.RejectInput, false);
				return;
			}
			choicePart.Choose(choice);
		}
		SoundDefOf.Quest_Accepted.PlayOneShotOnCamera();
		quest.Accept(accepterPawn);
		window.Select(quest);
		Messages.Message("MessageQuestAccepted".Translate(accepterPawn, quest.name), accepterPawn, MessageTypeDefOf.TaskCompletion, false);
	}

	private static void InvokeAcceptQuestByInterface(MainTabWindow_Quests window, Action? preAcceptAction, bool requiresAccepter)
		=> _acceptQuestByInterfaceMethod.Invoke(window, [preAcceptAction, requiresAccepter]);

	private static void ScheduleAcceptanceByInterface(Quest quest, int? index, DelayedQuestAcceptanceDraft draft) {
		bool requiresAccepter = index is { } idx ? RequiresAccepter(quest, idx) : quest.RequiresAccepter;
		if (!requiresAccepter) {
			ScheduleAcceptance(quest, index, null, draft);
			return;
		}
		var list = new List<FloatMenuOption>();
		foreach (var pawn in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists_NoSuspended) {
			if (!QuestUtility.CanPawnAcceptQuest(pawn, quest))
				continue;
			var pawnLocal = pawn;
			string text = "AcceptWith".Translate(pawn);
			if (pawn.royalty != null && pawn.royalty.AllTitlesInEffectForReading.Any())
				text += " (" + pawn.royalty.MostSeniorTitle.def.GetLabelFor(pawnLocal) + ")";
			list.Add(
				new FloatMenuOption(
					text,
					() => {
						if (!QuestUtility.CanPawnAcceptQuest(pawnLocal, quest))
							return;
						void ScheduleAction() => ScheduleAcceptance(quest, index, pawnLocal, draft);
						if (TryGetRoyalFavorAccepterWarning(quest, pawnLocal, out string warning))
							Find.WindowStack.Add(new Dialog_MessageBox(warning, "Confirm".Translate(), ScheduleAction, "GoBack".Translate()));
						else
							ScheduleAction();
					}
				)
			);
		}
		if (list.Count > 0)
			Find.WindowStack.Add(new FloatMenu(list));
		else
			Messages.Message("MessageNoColonistCanAcceptQuest".Translate(Faction.OfPlayer.def.pawnsPlural), MessageTypeDefOf.RejectInput, false);
	}

	private static bool TryGetRoyalFavorAccepterWarning(Quest quest, Pawn pawn, out string warning) {
		warning = string.Empty;
		var royalFavorPart = quest.PartsListForReading.OfType<QuestPart_GiveRoyalFavor>().FirstOrDefault();
		if (royalFavorPart is not { giveToAccepter: true })
			return false;
		var conceitedTraits = RoyalTitleUtility.GetConceitedTraits(pawn).ToList();
		var negativePsylinkTraits = RoyalTitleUtility.GetTraitsAffectingPsylinkNegatively(pawn).ToList();
		bool socialDisabled = pawn.skills.GetSkill(SkillDefOf.Social).TotallyDisabled;
		bool hasNegativePsylinkTraits = !pawn.HasPsylink && negativePsylinkTraits.Count > 0;
		if (!socialDisabled && conceitedTraits.Count == 0 && !hasNegativePsylinkTraits)
			return false;
		var pawnArg = pawn.Named("PAWN");
		var factionArg = royalFavorPart.faction.Named("FACTION");
		string text = "QuestGivesRoyalFavor".Translate(pawnArg, factionArg);
		if (socialDisabled)
			text += "\n\n" + "RoyalIncapableOfSocial".Translate(pawnArg, factionArg);
		if (conceitedTraits.Count > 0) {
			text += "\n\n"
				+ "RoyalWithConceitedTrait".Translate(
					pawnArg,
					factionArg,
					conceitedTraits.Select(trait => trait.Label).ToCommaList(true)
				);
		}
		if (hasNegativePsylinkTraits) {
			text += "\n\n"
				+ "RoyalWithTraitAffectingPsylinkNegatively".Translate(
					pawnArg,
					factionArg,
					negativePsylinkTraits.Select(trait => trait.Label).ToCommaList(true)
				);
		}
		warning = text + "\n\n" + "WantToContinue".Translate();
		return true;
	}

	private static void ScheduleAcceptance(Quest quest, int? choiceIndex, Pawn? accepterPawn, DelayedQuestAcceptanceDraft draft) {
		var result = Manager.Schedule(quest, choiceIndex, accepterPawn, draft, out var schedule, out string? error);
		if (result != DelayedQuestAcceptanceScheduleResult.Invalid && schedule is not null)
			ShowScheduledMessage(result, schedule.FireTick);
		else if (!error.NullOrEmpty())
			Messages.Message(error, MessageTypeDefOf.RejectInput, false);
	}

	private static string GetRewardActionLabel(Quest quest) {
		var draft = Manager.GetDraft(quest);
		return draft.Enabled ? Translate("Buttons.ScheduleAcceptFor") : "AcceptQuestFor".Translate() + ":";
	}

	private static float GetRewardActionWidth(string label) => Mathf.Clamp(Text.CalcSize(label).x + 34f, 110f, 190f);

	private static string GetActionTooltip(
		AcceptanceReport acceptanceReport,
		bool delayed,
		bool validSchedule,
		int fireTick,
		string? scheduleError,
		bool rewardChoice
	) {
		string tip = rewardChoice ? "AcceptQuestForTip".Translate() : "AcceptQuest".Translate();
		if (delayed) {
			string extra = validSchedule
				? GetScheduledTooltip(fireTick)
				: (scheduleError ?? string.Empty).Colorize(ColorLibrary.RedReadable);
			return $"{tip}\n\n{extra}";
		}
		return !acceptanceReport.Reason.NullOrEmpty()
			? $"{tip}\n\n{acceptanceReport.Reason.Colorize(rewardChoice ? ColorLibrary.RedReadable : ColoredText.WarningColor)}"
			: tip;
	}

	private static void GetScheduledActionRects(Rect innerRect, out Rect cancelRect, out Rect acceptNowRect) {
		var dismissRect = new Rect(innerRect.xMax - 32f - 4f, innerRect.y, 32f, 32f);
		acceptNowRect = new Rect(
			dismissRect.x - _TOP_ICON_GAP - _TOP_ICON_SIZE,
			dismissRect.y + (dismissRect.height - _TOP_ICON_SIZE) / 2f,
			_TOP_ICON_SIZE,
			_TOP_ICON_SIZE
		);
		cancelRect = new Rect(acceptNowRect.x - _TOP_ICON_GAP - _TOP_ICON_SIZE, acceptNowRect.y, _TOP_ICON_SIZE, _TOP_ICON_SIZE);
	}

	private static Rect GetCharityIconRect(Rect innerRect, bool scheduled) {
		float extraOffset = scheduled ? _TOP_ICON_SIZE * 2f + _TOP_ICON_GAP * 2f : 0f;
		return new Rect(innerRect.xMax - 32f - 26f - 32f - 4f - extraOffset, innerRect.y, 32f, 32f);
	}

	private static void ShowScheduledMessage(DelayedQuestAcceptanceScheduleResult result, int fireTick) {
		string key = result == DelayedQuestAcceptanceScheduleResult.Created ? "Messages.ScheduledCreated" : "Messages.ScheduledReplaced";
		Messages.Message(
			Translate(key, Math.Max(fireTick - Find.TickManager.TicksGame, 0).ToStringTicksToPeriod()),
			MessageTypeDefOf.TaskCompletion,
			false
		);
	}
}