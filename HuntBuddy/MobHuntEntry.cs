using System.Text.Json.Serialization;

namespace HuntDate;

public class MobHuntEntry {
	public string? Name { get; init; }

	public string? TerritoryName { get; init; }

	public string? ExpansionName { get; init; }

	public uint ExpansionId { get; init; }

	public uint MapId { get; init; }

	public uint TerritoryType { get; init; }

	public uint MobHuntId { get; init; }

	public bool IsEliteMark { get; init; }

	public byte BillNumber { get; set; }

	public byte MobIndex { get; set; }

	public uint NeededKills { get; set; }

	/// <summary>
	/// For own marks: stamped with the live kill count at share time.
	/// For external (date's) marks: the count sent by the sharer, incremented locally when party kills are detected.
	/// </summary>
	public uint KillCount { get; set; }

	public uint Icon { get; init; }

	[JsonIgnore] public bool IsExternal { get; set; }
}
