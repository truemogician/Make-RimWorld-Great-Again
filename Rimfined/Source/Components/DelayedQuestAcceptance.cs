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

public enum DelayedQuestAcceptanceScheduleResult : byte {
	Invalid,
	Created,
	Replaced
}

public readonly record struct DelayedQuestAcceptanceSpec(int Hours, bool BeforeExpiration) {
	public static readonly IReadOnlyDictionary<DelayedQuestAcceptancePreset, DelayedQuestAcceptanceSpec> PresetSpecs =
		new Dictionary<DelayedQuestAcceptancePreset, DelayedQuestAcceptanceSpec> {
			[DelayedQuestAcceptancePreset.OneDayAfter] = new(24, false),
			[DelayedQuestAcceptancePreset.OneDayBeforeExpiration] = new(24, true),
			[DelayedQuestAcceptancePreset.RightBeforeExpiration] = new(1, true)
		};

	public DelayedQuestAcceptancePreset Preset {
		get {
			foreach (var kv in PresetSpecs) {
				if (kv.Value == this)
					return kv.Key;
			}
			return DelayedQuestAcceptancePreset.Custom;
		}
	}
}

public sealed class DelayedQuestAcceptanceManager : GameComponent {
	private List<DelayedQuestAcceptanceSchedule> _schedules = [];

	private readonly Dictionary<int, DelayedQuestAcceptanceDraft> _drafts = [];

	public DelayedQuestAcceptanceManager(Game game) { }

	public DelayedQuestAcceptanceDraft GetDraft(Quest quest) {
		if (!_drafts.TryGetValue(quest.id, out var draft))
			_drafts[quest.id] = draft = DelayedQuestAcceptanceDraft.DefaultFor(quest);
		return draft;
	}

	public bool TryGetSchedule(Quest quest, [NotNullWhen(true)] out DelayedQuestAcceptanceSchedule? schedule) {
		schedule = _schedules.FirstOrDefault(entry => entry.Quest == quest);
		if (schedule is null)
			return false;
		if (schedule.IsStale) {
			_schedules.Remove(schedule);
			schedule = null;
			return false;
		}
		return true;
	}

	public void SetDraft(Quest quest, DelayedQuestAcceptanceDraft draft) => _drafts[quest.id] = draft;

	public DelayedQuestAcceptanceScheduleResult Schedule(
		Quest quest,
		int? choiceIndex,
		Pawn? accepter,
		DelayedQuestAcceptanceDraft draft,
		out DelayedQuestAcceptanceSchedule? schedule,
		out string? error
	) {
		schedule = null;
		error = null;
		if (!TryGetScheduledFireTick(quest, draft, out int fireTick, out error))
			return DelayedQuestAcceptanceScheduleResult.Invalid;
		bool replaced = TryGetSchedule(quest, out schedule);
		schedule ??= new DelayedQuestAcceptanceSchedule(quest);
		schedule.FireTick = fireTick;
		schedule.ChoiceIndex = choiceIndex ?? -1;
		schedule.Accepter = accepter;
		schedule.Hours = draft.Hours;
		schedule.BeforeExpiration = draft.BeforeExpiration;
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
			if (schedule is null || schedule.IsStale) {
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
			foreach (var schedule in _schedules) {
				if (schedule is not null && GetReminderTick(schedule.FireTick) < now)
					schedule.ReminderSent = true;
			}
			_drafts.Clear();
		}
	}
}

public sealed class DelayedQuestAcceptanceSchedule(Quest quest) : IExposable {
	public Quest Quest = quest;

	public int FireTick;

	public int ChoiceIndex = -1;

	public Pawn? Accepter;

	public bool ReminderSent;

	public int Hours = 24;

	public bool BeforeExpiration;

	public void ExposeData() {
		Scribe_References.Look(ref Quest, "quest");
		Scribe_Values.Look(ref FireTick, "fireTick");
		Scribe_Values.Look(ref ChoiceIndex, "choiceIndex", -1);
		Scribe_References.Look(ref Accepter, "accepter");
		Scribe_Values.Look(ref ReminderSent, "reminderSent");
		Scribe_Values.Look(ref Hours, "hours", 24);
		Scribe_Values.Look(ref BeforeExpiration, "beforeExpiration");
	}

	public DelayedQuestAcceptanceSpec Spec => new(Hours, BeforeExpiration);

	public DelayedQuestAcceptancePreset Preset => Spec.Preset;

	public bool IsStale => Quest.State != QuestState.NotYetAccepted || FireTick <= 0;
}

public sealed class DelayedQuestAcceptanceDraft {
	private const int _MAX_AMOUNT = 9999;

	public bool Enabled;

	public int Amount = 1;

	public string? AmountBuffer = "1";

	public bool IsDay = true;

	public bool BeforeExpiration;

	public int Hours => Amount * (IsDay ? 24 : 1);

	public DelayedQuestAcceptanceSpec Spec => new(Hours, BeforeExpiration);

	public DelayedQuestAcceptancePreset Preset => Spec.Preset;

	public static DelayedQuestAcceptanceDraft DefaultFor(Quest quest) {
		var draft = new DelayedQuestAcceptanceDraft();
		draft.ApplyPreset(DelayedQuestAcceptancePreset.OneDayAfter, quest);
		return draft;
	}

	public static DelayedQuestAcceptanceDraft FromSchedule(DelayedQuestAcceptanceSchedule schedule) {
		(int amount, bool isDay) = DecomposeHours(schedule.Hours);
		return new DelayedQuestAcceptanceDraft {
			Enabled = true,
			Amount = amount,
			AmountBuffer = amount.ToString(),
			IsDay = isDay,
			BeforeExpiration = schedule.BeforeExpiration
		};
	}

	public void ApplyPreset(DelayedQuestAcceptancePreset preset, Quest quest) {
		if (!DelayedQuestAcceptanceSpec.PresetSpecs.TryGetValue(preset, out var spec))
			return;
		(Amount, IsDay) = DecomposeHours(spec.Hours);
		BeforeExpiration = spec.BeforeExpiration;
		NormalizeFor(quest);
	}

	public void NormalizeFor(Quest quest) {
		Amount = Math.Clamp(Amount, 1, _MAX_AMOUNT);
		AmountBuffer ??= Amount.ToString();
		if (quest.acceptanceExpireTick < 0)
			BeforeExpiration = false;
	}

	private static (int Amount, bool IsDay) DecomposeHours(int hours) =>
		hours % 24 == 0
			? (hours / 24, true)
			: (hours, false);
}

internal static class DelayedQuestAcceptanceUtility {
	private const string _TRANSLATION_KEY_PREFIX = "Rimfined.DelayedQuestAcceptance";

	internal static string? LastFailureMessage { get; private set; }

	internal static DelayedQuestAcceptanceManager Manager => CachedGameComponent<DelayedQuestAcceptanceManager>.Component;

	internal static string Translate(string suffix) => $"{_TRANSLATION_KEY_PREFIX}.{suffix}".Translate().Resolve();

	internal static string Translate(string suffix, params object[] args) {
		string text = Translate(suffix);
		return args.Length == 0 ? text : string.Format(text, args);
	}

	internal static QuestPart_Choice? GetChoicePart(Quest quest) => quest.PartsListForReading.OfType<QuestPart_Choice>().FirstOrDefault();

	internal static bool TryResolveChoice(
		Quest quest,
		int index,
		[NotNullWhen(true)] out QuestPart_Choice? choicePart,
		[NotNullWhen(true)] out QuestPart_Choice.Choice? choice
	) {
		choicePart = GetChoicePart(quest);
		choice = null;
		if (choicePart?.choices is not { Count: > 0 } choices)
			return false;
		if (index < 0 || index >= choices.Count)
			return false;
		choice = choices[index];
		return true;
	}

	internal static bool RequiresAccepter(Quest quest, int? index = null) {
		if (index is not { } idx || !TryResolveChoice(quest, idx, out var choicePart, out var selectedChoice))
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

	internal static string GetPresetLabel(DelayedQuestAcceptancePreset preset) => Translate($"Preset.{preset}");

	internal static string GetUnitLabel(bool isDay) => Translate(isDay ? "Unit.Day" : "Unit.Hour");

	internal static string GetDirectionLabel(bool beforeExpiration)
		=> Translate(beforeExpiration ? "Direction.BeforeExpiration" : "Direction.SinceNow");

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
		int delta = checked(draft.Hours * GenDate.TicksPerHour);
		int targetTick;
		if (!draft.BeforeExpiration)
			targetTick = now + delta;
		else {
			if (quest.acceptanceExpireTick < 0) {
				error = Translate("Errors.NeedsExpiration");
				return false;
			}
			targetTick = quest.acceptanceExpireTick - delta;
		}
		if (targetTick <= now) {
			error = Translate("Errors.TimePassed");
			return false;
		}
		fireTick = RoundUpToHour(targetTick);
		if (quest.acceptanceExpireTick >= 0 && fireTick >= quest.acceptanceExpireTick) {
			error = Translate("Errors.AfterExpiration");
			return false;
		}
		return true;
	}

	internal static int GetReminderTick(int fireTick) => fireTick - 2 * GenDate.TicksPerHour;

	internal static void TryNotifyReminder(DelayedQuestAcceptanceSchedule schedule, int now) {
		if (schedule.ReminderSent || schedule.FireTick <= now)
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
		var report = QuestUtility.CanAcceptQuest(quest);
		if (!report.Accepted) {
			LastFailureMessage = Translate("Messages.CanceledFailed", quest.name, report.Reason);
			return false;
		}
		bool requiresAccepter = RequiresAccepter(quest, schedule.ChoiceIndex >= 0 ? schedule.ChoiceIndex : null);
		Pawn? accepter = null;
		if (requiresAccepter) {
			accepter = schedule.Accepter;
			if (accepter is null) {
				LastFailureMessage = Translate("Messages.CanceledRequiresAccepter", quest.name);
				return false;
			}
			if (!QuestUtility.CanPawnAcceptQuest(accepter, quest)) {
				LastFailureMessage = Translate("Messages.CanceledAccepterUnavailable", quest.name, accepter.LabelShortCap);
				return false;
			}
		}
		if (schedule.ChoiceIndex >= 0 && TryResolveChoice(quest, schedule.ChoiceIndex, out var choicePart, out var choice))
			choicePart.Choose(choice);
		quest.Accept(accepter);
		return true;
	}
}
