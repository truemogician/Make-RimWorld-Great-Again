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

	private static readonly Color _cancelIconColor = new(1f, 0.84f, 0.84f);

	private static readonly Color _acceptNowIconColor = new(0.84f, 1f, 0.84f);

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
		float nextY = baseY;
		if (choicePart is null && !scheduled) {
			DrawAcceptButtonRow(__instance, quest, new Rect(innerRect.x, nextY, innerRect.width, _ROW_HEIGHT));
			nextY += _ROW_HEIGHT + _ROW_GAP;
			DrawControlStrip(quest, new Rect(innerRect.x, nextY, innerRect.width, _ROW_HEIGHT));
			nextY += _ROW_HEIGHT + _ROW_GAP;
		}

		if (Prefs.DevMode) {
			float devY = choicePart is null && !scheduled ? nextY : baseY;
			DrawDevAcceptButton(quest, new Rect(innerRect.x, devY, 180f, _ROW_HEIGHT));
			nextY = devY + _ROW_HEIGHT + _ROW_GAP;
		}

		if (choicePart is null) {
			if (!scheduled || Prefs.DevMode)
				curY = nextY;
		}
		else if (Prefs.DevMode)
			curY = nextY;
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
			if (
				totalValue > 0f
				&& (
					choicePart.choices[j].rewards.Count != 1
					|| choicePart.choices[j].rewards[0] is not Reward_Items { items: not null } rewardItems
					|| rewardItems.items.Count != 1
					|| rewardItems.items[0].StyleSourcePrecept is not Precept_Relic
				)
			) {
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

			if (showAcceptButtons) {
				var acceptRect = new Rect(rect.x, rect.y, actionWidth, rect.height);
				var acceptanceReport = QuestUtility.CanAcceptQuest(quest);
				var draft = Manager.GetDraft(quest);
				bool delayed = draft.Enabled;
				bool validSchedule = TryGetScheduledFireTick(quest, draft, out int fireTick, out string? scheduleError);
				if ((!delayed && !acceptanceReport.Accepted) || (delayed && !validSchedule))
					GUI.color = Color.grey;
				if (Widgets.ButtonText(acceptRect, actionLabel)) {
					if (!delayed)
						AcceptNow(__instance, quest, j);
					else
						ScheduleAcceptance(quest, j, draft);
				}
				TooltipHandler.TipRegion(acceptRect, GetActionTooltip(acceptanceReport, delayed, validSchedule, fireTick, scheduleError, true));
				GUI.color = Color.white;
			}

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
		if (Widgets.ButtonImage(cancelRect, TexButton.Suspend, _cancelIconColor, true, Translate("Buttons.Cancel"))) {
			if (Manager.CancelSchedule(quest))
				Messages.Message(Translate("Messages.ScheduledCanceled"), MessageTypeDefOf.TaskCompletion, false);
		}
		if (Widgets.ButtonImage(
			acceptNowRect,
			TexButton.Play,
			_acceptNowIconColor,
			true,
			Translate("Buttons.AcceptNow")
		))
			AcceptNow(__instance, quest, schedule.ChoiceIndex >= 0 ? schedule.ChoiceIndex : null);
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
			tip = string.Join(
				"\n",
				"QuestExpiresOn".Translate(
					GenDate.DateFullStringWithHourAt(Find.TickManager.TicksAbs + quest.TicksUntilExpiry, QuestUtility.GetLocForDates())
				),
				tip
			);
		}
		num += Text.LineHeight;

		var anchor = Text.Anchor;
		GUI.color = _timeInfoColor;
		Text.Anchor = TextAnchor.MiddleRight;
		Widgets.Label(rect, text);
		GUI.color = Color.white;
		Text.Anchor = anchor;
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
		bool delayed = draft.Enabled;
		string label = delayed ? Translate("Buttons.ScheduleAccept") : "AcceptQuest".Translate();
		float width = Mathf.Min(row.width, Mathf.Max(180f, Text.CalcSize(label).x + 32f));
		var acceptRect = new Rect(row.x, row.y, width, row.height);
		var acceptanceReport = QuestUtility.CanAcceptQuest(quest);
		bool validSchedule = TryGetScheduledFireTick(quest, draft, out int fireTick, out string? scheduleError);
		if ((!delayed && !acceptanceReport.Accepted) || (delayed && !validSchedule))
			GUI.color = Color.grey;
		if (Widgets.ButtonText(acceptRect, label)) {
			if (!delayed)
				AcceptNow(window, quest, null);
			else
				ScheduleAcceptance(quest, null, draft);
		}
		TooltipHandler.TipRegion(acceptRect, GetActionTooltip(acceptanceReport, delayed, validSchedule, fireTick, scheduleError, false));
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
			_ => draft.Unit,
			_ => GenerateUnitOptions(quest, draft),
			GetUnitLabel(draft.Unit)
		);
		if (quest.acceptanceExpireTick < 0) {
			Widgets.ButtonText(
				rects[4],
				GetDirectionLabel(DelayedQuestAcceptanceDirection.SinceNow),
				true,
				false,
				false
			);
		}
		else {
			Widgets.Dropdown(
				rects[4],
				quest,
				_ => draft.Direction,
				_ => GenerateDirectionOptions(quest, draft),
				GetDirectionLabel(draft.Direction)
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
		draft.Preset = DelayedQuestAcceptancePreset.Custom;
		Manager.SetDraft(quest, draft);
	}

	private static IEnumerable<Widgets.DropdownMenuElement<DelayedQuestAcceptancePreset>> GeneratePresetOptions(
		Quest quest,
		DelayedQuestAcceptanceDraft draft
	) {
		foreach (DelayedQuestAcceptancePreset preset in Enum.GetValues(typeof(DelayedQuestAcceptancePreset))) {
			bool allowed = quest.acceptanceExpireTick >= 0
				|| preset is not (DelayedQuestAcceptancePreset.OneDayBeforeExpiration or DelayedQuestAcceptancePreset.RightBeforeExpiration);
			yield return new Widgets.DropdownMenuElement<DelayedQuestAcceptancePreset> {
				payload = preset,
				option = new FloatMenuOption(
					GetPresetLabel(preset),
					allowed
						? () => {
							draft.ApplyPreset(preset, quest);
							Manager.SetDraft(quest, draft);
						}
						: null
				)
			};
		}
	}

	private static IEnumerable<Widgets.DropdownMenuElement<DelayedQuestAcceptanceUnit>> GenerateUnitOptions(
		Quest quest,
		DelayedQuestAcceptanceDraft draft
	) {
		foreach (DelayedQuestAcceptanceUnit unit in Enum.GetValues(typeof(DelayedQuestAcceptanceUnit))) {
			yield return new Widgets.DropdownMenuElement<DelayedQuestAcceptanceUnit> {
				payload = unit,
				option = new FloatMenuOption(
					GetUnitLabel(unit),
					() => {
						draft.Unit = unit;
						draft.Preset = DelayedQuestAcceptancePreset.Custom;
						Manager.SetDraft(quest, draft);
					}
				)
			};
		}
	}

	private static IEnumerable<Widgets.DropdownMenuElement<DelayedQuestAcceptanceDirection>> GenerateDirectionOptions(
		Quest quest,
		DelayedQuestAcceptanceDraft draft
	) {
		foreach (DelayedQuestAcceptanceDirection direction in Enum.GetValues(typeof(DelayedQuestAcceptanceDirection))) {
			bool allowed = quest.acceptanceExpireTick >= 0 || direction != DelayedQuestAcceptanceDirection.BeforeExpiration;
			yield return new Widgets.DropdownMenuElement<DelayedQuestAcceptanceDirection> {
				payload = direction,
				option = new FloatMenuOption(
					GetDirectionLabel(direction),
					allowed
						? () => {
							draft.Direction = direction;
							draft.Preset = DelayedQuestAcceptancePreset.Custom;
							draft.NormalizeFor(quest);
							Manager.SetDraft(quest, draft);
						}
						: null
				)
			};
		}
	}

	private static void AcceptNow(MainTabWindow_Quests window, Quest quest, int? choiceIndex) {
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

	private static void InvokeAcceptQuestByInterface(MainTabWindow_Quests window, Action? preAcceptAction, bool requiresAccepter)
		=> _acceptQuestByInterfaceMethod.Invoke(window, [preAcceptAction, requiresAccepter]);

	private static void ScheduleAcceptance(Quest quest, int? choiceIndex, DelayedQuestAcceptanceDraft draft) {
		var result = Manager.Schedule(quest, choiceIndex, draft, out var schedule, out string? error);
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
		return delayed switch {
			false when !acceptanceReport.Reason.NullOrEmpty() => $"{tip}\n\n"
				+ acceptanceReport.Reason.Colorize(rewardChoice ? ColorLibrary.RedReadable : ColoredText.WarningColor),
			true => $"{tip}\n\n"
				+ (validSchedule
					? GetScheduledTooltip(fireTick)
					: (scheduleError ?? string.Empty).Colorize(ColorLibrary.RedReadable)
				),
			_ => tip
		};
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
