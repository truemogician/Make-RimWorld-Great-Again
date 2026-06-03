using System.Runtime.CompilerServices;
using RimWorld;
using UnityEngine;
using Verse;

namespace TrueMogician.RimWorld.WorkMemory;

public static class WorkMemoryCurve {
	public const float DEFAULT_PENALTY = 0.3f;

	public const float DEFAULT_WARMUP_SPEED = 1f;

	public const int DEFAULT_DECAY_DELAY = 1 * GenDate.TicksPerDay;

	public const float DEFAULT_DECAY_SPEED = 0.25f;

	public const float MIN_REFERENCE_WORK_AMOUNT = 200f;

	public const float MIDPOINT_FACTOR = 1f;

	public const float SLOPE_FACTOR = 0.2f;

	public const float MOMENTUM_CAP_FACTOR = 2f;

	public const float DEFAULT_PERMANENT_SCALE = 4f;

	public const float DEFAULT_PERMANENT_CURVATURE = 0.5f;

	public const float DEFAULT_PERMANENT_MAX_FRACTION = 1f;

	public static float MinMultiplier => Settings.Default is { } settings ? settings.MinMultiplier : 1f - DEFAULT_PENALTY;

	public static float MaxMultiplier => Settings.Default is { } settings ? settings.MaxMultiplier : 1f + DEFAULT_PENALTY * 0.5f;

	public static float WarmupSpeed => Settings.Default is { } settings ? settings.WarmupSpeed : DEFAULT_WARMUP_SPEED;

	public static int DecayDelay => Settings.Default is { } settings ? settings.DecayDelay : DEFAULT_DECAY_DELAY;

	public static float DecayPerTick => Mathf.Max(0f, Settings.Default is { } settings ? settings.DecaySpeed : DEFAULT_DECAY_SPEED);

	public static float PermanentScale => Settings.Default is { } settings ? settings.PermanentScale : DEFAULT_PERMANENT_SCALE;

	public static float PermanentCurvature => Settings.Default is { } settings ? settings.PermanentCurvature : DEFAULT_PERMANENT_CURVATURE;

	public static float PermanentMaxFraction => Settings.Default is { } settings ? settings.PermanentMaxFraction : DEFAULT_PERMANENT_MAX_FRACTION;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float GetMultiplier(float momentum, RecipeDef recipe) =>
		GetMultiplier(momentum, GetReferenceWorkAmount(recipe), MinMultiplier, MaxMultiplier);

	public static float GetMultiplier(float momentum, float referenceWorkAmount, float minMultiplier, float maxMultiplier) {
		float midpoint = referenceWorkAmount * MIDPOINT_FACTOR;
		float slope = referenceWorkAmount * SLOPE_FACTOR;
		float momentumCap = GetMomentumCap(referenceWorkAmount);
		float lowerBound = RawSigmoid(0f, midpoint, slope);
		float upperBound = RawSigmoid(momentumCap, midpoint, slope);
		float normalized = Mathf.InverseLerp(lowerBound, upperBound, RawSigmoid(Mathf.Clamp(momentum, 0f, momentumCap), midpoint, slope));
		return Mathf.Lerp(minMultiplier, maxMultiplier, normalized);
	}

	public static float GetMomentumForMultiplier(float multiplier, float referenceWorkAmount, float minMultiplier, float maxMultiplier) {
		float momentumCap = GetMomentumCap(referenceWorkAmount);
		float midpoint = referenceWorkAmount * MIDPOINT_FACTOR;
		float slope = referenceWorkAmount * SLOPE_FACTOR;
		float lowerBound = RawSigmoid(0f, midpoint, slope);
		float upperBound = RawSigmoid(momentumCap, midpoint, slope);
		float normalized = Mathf.InverseLerp(minMultiplier, maxMultiplier, multiplier);
		float raw = Mathf.Lerp(lowerBound, upperBound, normalized);
		raw = Mathf.Clamp(raw, 0.0001f, 0.9999f);
		return Mathf.Clamp(midpoint + slope * Mathf.Log(raw / (1f - raw)), 0f, momentumCap);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float GetMomentumCap(RecipeDef recipe) => GetMomentumCap(GetReferenceWorkAmount(recipe));

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float GetMomentumCap(float referenceWorkAmount) => referenceWorkAmount * MOMENTUM_CAP_FACTOR;

	/// <summary>
	///     Non-decaying momentum floor built from lifetime cumulative work, following the power law of forgetting/practice:
	///     <c>floor = cap * pMax * (1 - (1 + W / tau)^(-beta))</c>. Big early gains with a heavy tail toward full mastery.
	/// </summary>
	public static float GetPermanentMomentum(float cumulativeWork, float referenceWorkAmount) =>
		GetPermanentMomentum(cumulativeWork, referenceWorkAmount, PermanentScale, PermanentCurvature, PermanentMaxFraction);

	/// <inheritdoc cref="GetPermanentMomentum(float,float)" />
	public static float GetPermanentMomentum(float cumulativeWork, float referenceWorkAmount, float scale, float curvature, float maxFraction) {
		if (cumulativeWork <= 0f)
			return 0f;
		float tau = referenceWorkAmount * scale;
		float fraction = maxFraction * (1f - Mathf.Pow(1f + cumulativeWork / tau, -curvature));
		return GetMomentumCap(referenceWorkAmount) * fraction;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float GetReferenceWorkAmount(float recipeWorkAmount, float warmupSpeed) {
		float amount = Mathf.Max(recipeWorkAmount, MIN_REFERENCE_WORK_AMOUNT);
		return amount / Mathf.Max(warmupSpeed, 0.01f);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float GetReferenceWorkAmount(RecipeDef recipe) => GetReferenceWorkAmount(recipe.WorkAmountTotal(null), WarmupSpeed);

	private static float RawSigmoid(float momentum, float midpoint, float slope) => 1f / (1f + Mathf.Exp(-(momentum - midpoint) / slope));
}