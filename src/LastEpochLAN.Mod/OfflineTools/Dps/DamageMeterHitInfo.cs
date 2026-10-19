using Il2Cpp;

namespace LastEpochLAN.Mod.OfflineTools.Dps;

internal readonly struct DamageMeterHitInfo(string sourceName, string abilityName, string primaryAbilityName, string originalAbilityName, string abilityId, AT tags, HitEvents hitEvents, DamageType? primaryDamageType)
{
	public string SourceName { get; } = sourceName;

	public string AbilityName { get; } = abilityName;

	public string PrimaryAbilityName { get; } = primaryAbilityName;

	public string OriginalAbilityName { get; } = originalAbilityName;

	public string AbilityId { get; } = abilityId;

	public AT Tags { get; } = tags;

	public HitEvents HitEvents { get; } = hitEvents;

	public DamageType? PrimaryDamageType { get; } = primaryDamageType;
}
