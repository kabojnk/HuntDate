using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

using FFXIVClientStructs.FFXIV.Client.Game.UI;

using HuntDate.Utils;

namespace HuntDate.Windows;

/// <summary>
/// Main plugin window.
/// </summary>
public class MainWindow: Window {
	public MainWindow() : base(
		$"{Plugin.Instance.Name}",
		ImGuiWindowFlags.NoDocking,
		true) {
		this.Size = new Vector2(400 * ImGui.GetIO().FontGlobalScale, 500);
		this.SizeCondition = ImGuiCond.FirstUseEver;
		this.RespectCloseHotkey = !Plugin.Instance.Configuration.IgnoreCloseHotkey;
	}

	public override void PreOpenCheck() {
		if (Plugin.Instance.Configuration.LockWindowPositions) {
			this.Flags |= ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove;
		}
		else {
			this.Flags &= ~(ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove);
		}
	}

	public override unsafe void Draw() {
		if (!Plugin.Instance.MobHuntEntriesReady) {
			ImGui.Text("Reloading data ...");
			return;
		}

		ImGui.BeginGroup();
		ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.8f, 0.2f, 0.2f, 1));
		InterfaceUtil.DrawCenteredText("B-RANK AND ARR HUNT MARK");
		InterfaceUtil.DrawCenteredText("LOCATIONS ARE NOT SUPPORTED");
		ImGui.PopStyleColor();
		ImGui.EndGroup();
		if (ImGui.IsItemHovered()) {
			InterfaceUtil.DrawWrappedTooltip(ImGuiHelpers.GlobalScale * 400,
				"B-rank marks have a varying number of potential spawn locations, and will only ever exist in one of them at a time."
				+ $" {Plugin.Instance.Name} has no way to know which location a given mob is in, and as such cannot direct you to it."
				+ " You can look up spawn maps online to find the possible spots for your target.\n"
				+ "\n"
				+ "Several ARR hunt marks are FATE mobs, which means they aren't always available."
				+ $" Since {Plugin.Instance.Name} has no way to know if the FATE is up or not, ARR marks are not part of the plugin.");
		}

		if (InterfaceUtil.IconButton(FontAwesomeIcon.Redo, "Reload")) {
			Plugin.Instance.MobHuntEntriesReady = false;
			Task.Run(Plugin.Instance.ReloadData);
			return;
		}

		if (ImGui.IsItemHovered()) {
			ImGui.BeginTooltip();
			ImGui.Text("Click this button to reload daily hunt data");
			ImGui.EndTooltip();
		}

		ImGui.SameLine();

		if (InterfaceUtil.IconButton(FontAwesomeIcon.Cog, "Config")) {
			Plugin.Instance.OpenConfigUi();
		}

		ImGui.SameLine();

		if (ImGui.Button("Share")) {
			// Only share own marks that are not yet fully completed.
			List<MobHuntEntry> entriesToShare = Plugin.Instance.MobHuntEntries
				.SelectMany(exp => exp.Value.SelectMany(zone => zone.Value))
				.Where(e => !e.IsExternal &&
				            (e.IsEliteMark || (uint)MobHunt.Instance()->GetKillCount(e.BillNumber, e.MobIndex) < e.NeededKills))
				.ToList();

			// Stamp each entry with the live kill count so the recipient knows progress.
			foreach (MobHuntEntry e in entriesToShare) {
				e.KillCount = e.IsEliteMark ? 0u : (uint)MobHunt.Instance()->GetKillCount(e.BillNumber, e.MobIndex);
			}

			ImGui.SetClipboardText(JsonSerializer.Serialize(entriesToShare));
			ImGui.OpenPopup("HuntDate_Copied");
		}

		if (ImGui.IsItemHovered()) {
			ImGui.BeginTooltip();
			ImGui.Text("Copy incomplete hunt marks to clipboard (completed marks are excluded)");
			ImGui.EndTooltip();
		}

		ImGui.SameLine();

		if (ImGui.Button("Get Date's Marks From Clipboard")) {
			try {
				string json = ImGui.GetClipboardText();
				List<MobHuntEntry>? entries = JsonSerializer.Deserialize<List<MobHuntEntry>>(json);
				if (entries != null) {
					Plugin.Instance.ClearExternalEntries(); // replace any previous list outright
					Plugin.Instance.MergeExternalEntries(entries);
					ImGui.OpenPopup("HuntDate_Imported");
				}
			}
			catch (Exception ex) {
				Service.PluginLog.Error($"Failed to import hunt marks from clipboard: {ex}");
			}
		}

		if (ImGui.IsItemHovered()) {
			ImGui.BeginTooltip();
			ImGui.Text("Replace date's marks with the list currently in your clipboard");
			ImGui.EndTooltip();
		}

		ImGui.SameLine();

		if (ImGui.Button("Clear Date's List")) {
			Plugin.Instance.ClearExternalEntries();
		}

		if (ImGui.IsItemHovered()) {
			ImGui.BeginTooltip();
			ImGui.Text("Remove all imported date's hunt marks");
			ImGui.EndTooltip();
		}

		IEnumerable<KeyValuePair<string, Dictionary<KeyValuePair<uint, string>, List<MobHuntEntry>>>> expansionEntriesWithTreeNodes = Plugin.Instance
			.MobHuntEntries
			.Where(expansionEntry => ImGui.TreeNode(expansionEntry.Key));
		foreach (KeyValuePair<string, Dictionary<KeyValuePair<uint, string>, List<MobHuntEntry>>> expansionEntry in expansionEntriesWithTreeNodes) {
			IEnumerable<KeyValuePair<KeyValuePair<uint, string>, List<MobHuntEntry>>> mobEntriesWithTreeNodes = expansionEntry.Value
				.Where(entry => {
					bool treeOpen = ImGui.TreeNodeEx(entry.Key.Value, ImGuiTreeNodeFlags.AllowItemOverlap);
					ImGui.SameLine();
					int killedCount = entry.Value.Count(x => !x.IsExternal && MobHunt.Instance()->GetKillCount(x.BillNumber, x.MobIndex) == x.NeededKills);
					if (killedCount != entry.Value.Count) {
						ImGui.Text($"({killedCount}/{entry.Value.Count})");
					}
					else {
						ImGui.TextColored(
							new Vector4(0f, 1f, 0f, 1f),
							$"({killedCount}/{entry.Value.Count})");
					}
					return treeOpen;
				});
			foreach (KeyValuePair<KeyValuePair<uint, string>, List<MobHuntEntry>> entry in mobEntriesWithTreeNodes) {
				foreach (MobHuntEntry? mobHuntEntry in entry.Value) {
					if (Location.Database.ContainsKey(mobHuntEntry.MobHuntId)) {
						if (InterfaceUtil.IconButton(FontAwesomeIcon.MapMarkerAlt, $"pin##{mobHuntEntry.MobHuntId}")) {
							Location.CreateMapMarker(
								mobHuntEntry.TerritoryType,
								mobHuntEntry.MapId,
								mobHuntEntry.MobHuntId,
								mobHuntEntry.Name,
								Location.OpenType.None);
						}

						if (ImGui.IsItemHovered()) {
							ImGui.BeginTooltip();
							ImGui.Text("Place marker on the map");
							ImGui.EndTooltip();
						}

						ImGui.SameLine();

						if (InterfaceUtil.IconButton(FontAwesomeIcon.MapMarkedAlt, $"open##{mobHuntEntry.MobHuntId}")) {
							bool includeArea = Plugin.Instance.Configuration.IncludeAreaOnMap;
							if (ImGui.IsKeyDown(ImGuiKey.ModShift)) {
								includeArea = !includeArea;
							}

							Location.CreateMapMarker(
								mobHuntEntry.TerritoryType,
								mobHuntEntry.MapId,
								mobHuntEntry.MobHuntId,
								mobHuntEntry.Name,
								includeArea ? Location.OpenType.ShowOpen : Location.OpenType.MarkerOpen);
						}

						if (ImGui.IsItemHovered()) {
							Vector4 color = ImGui.IsKeyDown(ImGuiKey.ModShift)
								? new Vector4(0f, 0.7f, 0f, 1f)
								: new Vector4(0.7f, 0.7f, 0.7f, 1f);
							ImGui.BeginTooltip();
							if (Plugin.Instance.Configuration.IncludeAreaOnMap) {
								ImGui.Text("Show hunt area on the map");
								ImGui.TextColored(
									color,
									"Hold [SHIFT] to show the location only");
							}
							else {
								ImGui.Text("Show hunt location on the map");
								ImGui.TextColored(
									color,
									"Hold [SHIFT] to include the area");
							}

							ImGui.EndTooltip();
						}

						ImGui.SameLine();

						if (Plugin.TeleportConsumer?.IsAvailable == true) {
							if (InterfaceUtil.IconButton(FontAwesomeIcon.StreetView, $"teleport##{mobHuntEntry.MobHuntId}")) {
								Location.TeleportToNearestAetheryte(
									mobHuntEntry.TerritoryType,
									mobHuntEntry.MapId,
									mobHuntEntry.MobHuntId);
							}

							if (ImGui.IsItemHovered()) {
								ImGui.BeginTooltip();
								ImGui.Text("Teleport to nearest aetheryte");
								ImGui.EndTooltip();
							}

							ImGui.SameLine();
						}

						if (Plugin.Instance.Configuration.EnableXivEspIntegration && Plugin.EspConsumer?.IsAvailable == true) {
							if (InterfaceUtil.IconButton(FontAwesomeIcon.Search, $"esp##{mobHuntEntry.MobHuntId}")) {
								Plugin.EspConsumer.SearchFor(mobHuntEntry.Name!);
							}

							if (ImGui.IsItemHovered()) {
								ImGui.BeginTooltip();
								ImGui.Text("Set XivEsp search to this target");
								ImGui.EndTooltip();
							}

							ImGui.SameLine();
						}
					}

					int currentKills = mobHuntEntry.IsExternal
						? (int)mobHuntEntry.KillCount
						: MobHunt.Instance()->GetKillCount(mobHuntEntry.BillNumber, mobHuntEntry.MobIndex);
					ImGui.Text(mobHuntEntry.IsExternal ? $"{mobHuntEntry.Name} (date)" : mobHuntEntry.Name);
					if (ImGui.IsItemHovered()) {
						ImGui.PushStyleColor(ImGuiCol.PopupBg, Vector4.Zero);
						ImGui.BeginTooltip();
						InterfaceUtil.DrawHuntIcon(mobHuntEntry);
						ImGui.PopStyleColor();
						ImGui.EndTooltip();
					}

					ImGui.SameLine();
					if (currentKills >= (int)mobHuntEntry.NeededKills) {
						ImGui.TextColored(
							new Vector4(0f, 1f, 0f, 1f),
							$"({currentKills}/{mobHuntEntry.NeededKills})");
					}
					else {
						ImGui.Text($"({currentKills}/{mobHuntEntry.NeededKills})");
					}
				}

				ImGui.TreePop();
			}

			ImGui.TreePop();
		}

		// ── Confirmation modals ─────────────────────────────────────────────
		ImGui.SetNextWindowPos(
			new Vector2(ImGui.GetIO().DisplaySize.X / 2f, ImGui.GetIO().DisplaySize.Y / 2f),
			ImGuiCond.Appearing,
			new Vector2(0.5f, 0.5f));
		if (ImGui.BeginPopupModal("HuntDate_Copied", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar)) {
			InterfaceUtil.DrawCenteredText("Copied to clipboard!");
			ImGui.Spacing();
			float btnW = 80f * ImGui.GetIO().FontGlobalScale;
			ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - btnW) / 2f + ImGui.GetCursorPosX());
			if (ImGui.Button("OK", new Vector2(btnW, 0))) ImGui.CloseCurrentPopup();
			ImGui.EndPopup();
		}

		ImGui.SetNextWindowPos(
			new Vector2(ImGui.GetIO().DisplaySize.X / 2f, ImGui.GetIO().DisplaySize.Y / 2f),
			ImGuiCond.Appearing,
			new Vector2(0.5f, 0.5f));
		if (ImGui.BeginPopupModal("HuntDate_Imported", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar)) {
			InterfaceUtil.DrawCenteredText("Date's marks imported!");
			ImGui.Spacing();
			float btnW = 80f * ImGui.GetIO().FontGlobalScale;
			ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - btnW) / 2f + ImGui.GetCursorPosX());
			if (ImGui.Button("OK", new Vector2(btnW, 0))) ImGui.CloseCurrentPopup();
			ImGui.EndPopup();
		}
	}
}
