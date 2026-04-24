using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using RimWorld;
using TrueMogician.RimWorld.Utility;
using Verse;

namespace TrueMogician.RimWorld.Rimfined.Components;

using static DelayedQuestAcceptanceUtility;

public enum DelayedQuestAcceptancePreset : byte {
	Custom,
	OneDayAfter,
	OneDayBeforeExpiration,
	RightBeforeExpiration
}

public enum DelayedQuestAcceptanceUnit : byte {
	Hour,
	Day
}

public enum DelayedQuestAcceptanceDirection : byte {
	SinceNow,
	BeforeExpiration
}

public enum DelayedQuestAcceptanceScheduleResult : byte {
	Invalid,
	Created,
	Replaced
}

public sealed class DelayedQuestAcceptanceManager : GameComponent {
	private List<DelayedQuestAcceptanceSchedule> _schedules = [];

	private readonly Dictionary<int, DelayedQuestAcceptanceDraft> _drafts = [];

	public DelayedQuestAcceptanceManager(Game game) { }

	public DelayedQuestAcceptanceDraft GetDraft(Quest quest) {
		if (!_drafts.TryGetValue(quest.id, out var draft))
			_drafts[quest.id] = draft = DelayedQuestAcceptanceDraft.DefaultFor(quest);
		draft.NormalizeFor(quest);
		return draft;
	}

	public bool TryGetSchedule(Quest quest, [NotNullWhen(true)] out DelayedQuestAcceptanceSchedule? schedule) {
		schedule = _schedules.FirstOrDefault(entry => entry.Quest == quest);
		if (schedule is null)
			return false;
		if (ScheduleIsStale(schedule)) {
			_schedules.Remove(schedule);
			schedule = null;
			return false;
		}
		return true;
	}

	public void SetDraft(Quest quest, DelayedQuestAcceptanceDraft draft) {
		draft.NormalizeFor(quest);
		_drafts[quest.id] = draft;
	}

	public DelayedQuestAcceptanceScheduleResult Schedule(
		Quest quest,
		int? choiceIndex,
		DelayedQuestAcceptanceDraft draft,
		out DelayedQuestAcceptanceSchedule? schedule,
		out string? error
	) {
		schedule = null;
		error = null;
		draft.NormalizeFor(quest);
		if (!TryGetScheduledFireTick(quest, draft, out int fireTick, out error))
			return DelayedQuestAcceptanceScheduleResult.Invalid;
		bool replaced = TryGetSchedule(quest, out schedule);
		schedule ??= new DelayedQuestAcceptanceSchedule { Quest = quest };
		schedule.Quest = quest;
		schedule.FireTick = fireTick;
		schedule.ChoiceIndex = choiceIndex ?? -1;
		schedule.Preset = draft.Preset;
		schedule.Amount = draft.Amount;
		schedule.Unit = draft.Unit;
		schedule.Direction = draft.Direction;
		schedule.ReminderSent = false;
		TryNotifyReminder(schedule, Find.TickManager.TicksGame);
		_drafts[quest.id] = DelayedQuestAcceptanceDraft.FromSchedule(schedule);
		if (!replaced)
			_schedules.Add(schedule);
		return replaced ? DelayedQuestAcceptanceScheduleResult.Replaced : DelayedQuestAcceptanceScheduleResult.Created;
	}

	public bool CancelSchedule(Quest quest, bool keepDraft = true) {
		if (!TryGetSchedule(quest, out var schedule))
			return false;
		if (keepDraft)
			_drafts[quest.id] = DelayedQuestAcceptanceDraft.FromSchedule(schedule);
		_schedules.Remove(schedule);
		return true;
	}

	public override void GameComponentTick() {
		if (_schedules.Count == 0)
			return;
		int now = Find.TickManager.TicksGame;
		if (now <= 0 || now % GenDate.TicksPerHour != 0)
			return;
		for (int i = _schedules.Count - 1; i >= 0; i--) {
			var schedule = _schedules[i];
			if (schedule?.Quest is null || ScheduleIsStale(schedule)) {
				_schedules.RemoveAt(i);
				continue;
			}
			TryNotifyReminder(schedule, now);
			if (schedule.FireTick > now)
				continue;
			try {
				string? msg = TryExecuteScheduledAccept(schedule)
					? Translate("Messages.Accepted", schedule.Quest.name)
					: LastFailureMessage;
				_schedules.RemoveAt(i);
				if (!msg.NullOrEmpty()) {
					Messages.Message(
						msg,
						msg == LastFailureMessage ? MessageTypeDefOf.RejectInput : MessageTypeDefOf.TaskCompletion,
						false
					);
				}
			}
			catch (Exception ex) {
				Helper.Logger.Error($"Delayed quest acceptance failed: {ex}", true);
				_schedules.RemoveAt(i);
				Messages.Message(
					Translate("Messages.CanceledInvalid", schedule.Quest.name),
					MessageTypeDefOf.RejectInput,
					false
				);
			}
		}
	}

	public override void ExposeData() {
		Scribe_Collections.Look(ref _schedules, "delayedQuestAcceptanceSchedules", LookMode.Deep);
		if (Scribe.mode == LoadSaveMode.PostLoadInit) {
			_schedules ??= [];
			_schedules.RemoveAll(schedule => schedule?.Quest is null);
			int now = Find.TickManager?.TicksGame ?? 0;
			foreach (var schedule in _schedules)
				if (schedule is not null && GetReminderTick(schedule.FireTick) < now)
					schedule.ReminderSent = true;
			_drafts.Clear();
		}
	}
}

public sealed class DelayedQuestAcceptanceSchedule : IExposable {
	public Quest Quest = null!;

	public int FireTick;

	public int ChoiceIndex = -1;

	public bool ReminderSent;

	public DelayedQuestAcceptancePreset Preset;

	public int Amount = 1;

	public DelayedQuestAcceptanceUnit Unit = DelayedQuestAcceptanceUnit.Day;

	public DelayedQuestAcceptanceDirection Direction;

	public void ExposeData() {
		Scribe_References.Look(ref Quest, "quest");
		Scribe_Values.Look(ref FireTick, "fireTick");
		Scribe_Values.Look(ref ChoiceIndex, "choiceIndex", -1);
		Scribe_Values.Look(ref ReminderSent, "reminderSent");
		Scribe_Values.Look(ref Preset, "preset");
		Scribe_Values.Look(ref Amount, "amount", 1);
		Scribe_Values.Look(ref Unit, "unit");
		Scribe_Values.Look(ref Direction, "direction");
	}
}

public sealed class DelayedQuestAcceptanceDraft {
	private const int _MAX_AMOUNT = 9999;

	public bool Enabled;

	public DelayedQuestAcceptancePreset Preset = DelayedQuestAcceptancePreset.OneDayAfter;

	public int Amount = 1;

	public string? AmountBuffer = "1";

	public DelayedQuestAcceptanceUnit Unit = DelayedQuestAcceptanceUnit.Day;

	public DelayedQuestAcceptanceDirection Direction;

	public void ApplyPreset(DelayedQuestAcceptancePreset preset, Quest quest) {
		Preset = preset;
		switch (preset) {
			case DelayedQuestAcceptancePreset.Custom: break;
			case DelayedQuestAcceptancePreset.OneDayAfter:
				Amount = 1;
				Unit = DelayedQuestAcceptanceUnit.Day;
				Direction = DelayedQuestAcceptanceDirection.SinceNow;
				break;
			case DelayedQuestAcceptancePreset.OneDayBeforeExpiration:
				Amount = 1;
				Unit = DelayedQuestAcceptanceUnit.Day;
				Direction = DelayedQuestAcceptanceDirection.BeforeExpiration;
				break;
			case DelayedQuestAcceptancePreset.RightBeforeExpiration:
				Amount = 1;
				Unit = DelayedQuestAcceptanceUnit.Hour;
				Direction = DelayedQuestAcceptanceDirection.BeforeExpiration;
				break;
		}
		NormalizeFor(quest);
	}

	public void NormalizeFor(Quest quest) {
		Amount = Math.Clamp(Amount, 1, _MAX_AMOUNT);
		AmountBuffer ??= Amount.ToString();
		if (quest.acceptanceExpireTick < 0) {
			Direction = DelayedQuestAcceptanceDirection.SinceNow;
			if (Preset is DelayedQuestAcceptancePreset.OneDayBeforeExpiration or DelayedQuestAcceptancePreset.RightBeforeExpiration)
				Preset = DelayedQuestAcceptancePreset.Custom;
		}
	}

	public static DelayedQuestAcceptanceDraft DefaultFor(Quest quest) {
		var draft = new DelayedQuestAcceptanceDraft();
		draft.ApplyPreset(DelayedQuestAcceptancePreset.OneDayAfter, quest);
		return draft;
	}

	public static DelayedQuestAcceptanceDraft FromSchedule(DelayedQuestAcceptanceSchedule schedule) => new() {
		Enabled = true,
		Preset = schedule.Preset,
		Amount = schedule.Amount,
		AmountBuffer = schedule.Amount.ToString(),
		Unit = schedule.Unit,
		Direction = schedule.Direction
	};
}

internal static class DelayedQuestAcceptanceUtility {
	private const string _TRANSLATION_KEY_PREFIX = "Rimfined.DelayedQuestAcceptance";

	internal static DelayedQuestAcceptanceManager Manager => CachedGameComponent<DelayedQuestAcceptanceManager>.Component;

	internal static string? LastFailureMessage { get; private set; }

	internal static string Translate(string suffix) => $"{_TRANSLATION_KEY_PREFIX}.{suffix}".Translate().Resolve();

	internal static string Translate(string suffix, params object[] args) {
		string text = Translate(suffix);
		return args.Length == 0 ? text : string.Format(text, args);
	}

	internal static QuestPart_Choice? GetChoicePart(Quest quest) => quest.PartsListForReading.OfType<QuestPart_Choice>().FirstOrDefault();

	internal static bool ScheduleIsStale(DelayedQuestAcceptanceSchedule schedule) {
		if (schedule.Quest is null)
			return true;
		if (schedule.Quest.State != QuestState.NotYetAccepted)
			return true;
		return schedule.FireTick <= 0;
	}

	internal static bool TryResolveChoice(
		Quest quest,
		int choiceIndex,
		[NotNullWhen(true)] out QuestPart_Choice? choicePart,
		[NotNullWhen(true)] out QuestPart_Choice.Choice? choice
	) {
		choicePart = GetChoicePart(quest);
		choice = null;
		if (choicePart?.choices is not { Count: > 0 } choices)
			return false;
		if (choiceIndex < 0 || choiceIndex >= choices.Count)
			return false;
		choice = choices[choiceIndex];
		return true;
	}

	internal static bool RequiresAccepter(Quest quest, int? choiceIndex = null) {
		if (choiceIndex is not { } idx || !TryResolveChoice(quest, idx, out var choicePart, out var selectedChoice))
			return quest.RequiresAccepter;
		var remainingParts = quest.PartsListForReading.ToList();
		for (var i = 0; i < choicePart.choices.Count; i++) {
			if (i == idx)
				continue;
			foreach (var part in choicePart.choices[i].questParts) {
				if (!selectedChoice.questParts.Contains(part))
					remainingParts.Remove(part);
			}
		}
		return remainingParts.Any(part => part.RequiresAccepter);
	}

	internal static int GetUnitTicks(DelayedQuestAcceptanceUnit unit)
		=> unit == DelayedQuestAcceptanceUnit.Hour ? GenDate.TicksPerHour : GenDate.TicksPerDay;

	internal static string GetPresetLabel(DelayedQuestAcceptancePreset preset) => Translate($"Preset.{preset}");

	internal static string GetUnitLabel(DelayedQuestAcceptanceUnit unit) => Translate($"Unit.{unit}");

	internal static string GetDirectionLabel(DelayedQuestAcceptanceDirection direction) => Translate($"Direction.{direction}");

	internal static int RoundUpToHour(int tick) {
		if (tick <= 0)
			return 0;
		int remainder = tick % GenDate.TicksPerHour;
		return remainder == 0 ? tick : tick + GenDate.TicksPerHour - remainder;
	}

	internal static bool TryGetScheduledFireTick(Quest quest, DelayedQuestAcceptanceDraft draft, out int fireTick, out string? error) {
		fireTick = -1;
		error = null;
		draft.NormalizeFor(quest);
		int now = Find.TickManager.TicksGame;
		int delta = checked(draft.Amount * GetUnitTicks(draft.Unit));
		int targetTick;
		switch (draft.Direction) {
			case DelayedQuestAcceptanceDirection.SinceNow: targetTick = now + delta; break;
			case DelayedQuestAcceptanceDirection.BeforeExpiration:
				if (quest.acceptanceExpireTick < 0) {
					error = Translate("Errors.NeedsExpiration");
					return false;
				}
				targetTick = quest.acceptanceExpireTick - delta;
				break;
			default: throw new ArgumentOutOfRangeException(nameof(draft.Direction), draft.Direction, null);
		}
		if (targetTick <= now) {
			error = Translate("Errors.TimePassed");
			return false;
		}
		fireTick = RoundUpToHour(targetTick);
		if (fireTick <= now) {
			error = Translate("Errors.TimePassed");
			return false;
		}
		if (quest.acceptanceExpireTick >= 0 && fireTick >= quest.acceptanceExpireTick) {
			error = Translate("Errors.AfterExpiration");
			return false;
		}
		return true;
	}

	internal static int GetReminderTick(int fireTick) => fireTick - 2 * GenDate.TicksPerHour;

	internal static void TryNotifyReminder(DelayedQuestAcceptanceSchedule schedule, int now) {
		if (schedule.ReminderSent || schedule.Quest is null || schedule.FireTick <= now)
			return;
		int reminderTick = GetReminderTick(schedule.FireTick);
		if (reminderTick < now) {
			schedule.ReminderSent = true;
			return;
		}
		if (reminderTick != now)
			return;
		schedule.ReminderSent = true;
		Messages.Message(
			Translate("Messages.AcceptanceReminder", schedule.Quest.name, Math.Max(schedule.FireTick - now, 0).ToStringTicksToPeriod()),
			MessageTypeDefOf.NeutralEvent,
			false
		);
	}

	internal static string GetCountdownLabel(int fireTick) {
		int remainingTicks = Math.Max(fireTick - Find.TickManager.TicksGame, 0);
		return Translate("Countdown", remainingTicks.ToStringTicksToPeriod());
	}

	internal static string GetScheduledTooltip(int fireTick) {
		var date = GenDate.DateFullStringWithHourAt(GenDate.TickGameToAbs(fireTick), QuestUtility.GetLocForDates());
		return Translate("ScheduledTooltip", date);
	}

	internal static bool TryExecuteScheduledAccept(DelayedQuestAcceptanceSchedule schedule) {
		LastFailureMessage = null;
		if (schedule.Quest is not { } quest)
			return false;
		if (quest.State != QuestState.NotYetAccepted)
			return false;
		if (schedule.ChoiceIndex >= 0 && !TryResolveChoice(quest, schedule.ChoiceIndex, out _, out _)) {
			LastFailureMessage = Translate("Messages.CanceledInvalid", quest.name);
			return false;
		}
		var acceptanceReport = QuestUtility.CanAcceptQuest(quest);
		if (!acceptanceReport.Accepted) {
			LastFailureMessage = Translate("Messages.CanceledFailed", quest.name, acceptanceReport.Reason);
			return false;
		}
		if (RequiresAccepter(quest, schedule.ChoiceIndex >= 0 ? schedule.ChoiceIndex : null)) {
			LastFailureMessage = Translate("Messages.CanceledRequiresAccepter", quest.name);
			return false;
		}
		if (schedule.ChoiceIndex >= 0 && TryResolveChoice(quest, schedule.ChoiceIndex, out var choicePart, out var choice))
			choicePart.Choose(choice);
		quest.Accept(null);
		return true;
	}
}
