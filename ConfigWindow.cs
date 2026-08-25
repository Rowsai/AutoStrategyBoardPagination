using System.Numerics;
using AutoStrategyBoardPagination.Models;
using AutoStrategyBoardPagination.Services;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace AutoStrategyBoardPagination.Windows;

public sealed class ConfigWindow : Window {
    private readonly Configuration configuration;
    private readonly BoardService boards;
    private readonly TreeRunner runner;
    private Guid? selectedId;
    private List<string> boardNames = [];

    public ConfigWindow(Configuration configuration, BoardService boards, TreeRunner runner) : base("AutoStrategyBoardPagination 設定###asbp-config", ImGuiWindowFlags.NoScrollbar) {
        this.configuration = configuration;
        this.boards = boards;
        this.runner = runner;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(760, 500), MaximumSize = new Vector2(1300, 1000) };
    }

    public override void Draw() {
        PushDarkBlueTheme();
        try {
            DrawMode();
            ImGui.Separator();
            if (ImGui.Button("図面一覧を更新")) boardNames = boards.GetSavedBoardNames().ToList();
            ImGui.SameLine();
            if (ImGui.Button("ルートを追加")) { configuration.Roots.Add(NewNode()); Save(); }
            ImGui.SameLine();
            if (ImGui.Button("実行位置を先頭へ戻す")) runner.Reset();
            ImGui.Columns(2, "asbp-columns", true);
            DrawTree();
            ImGui.NextColumn();
            DrawEditorAndPreview();
            ImGui.Columns(1);
        } finally { PopDarkBlueTheme(); }
    }

    private void DrawMode() {
        var state = configuration.IsAutomationEnabled ? "AUTO" : configuration.IsEnabled ? "ON" : "OFF";
        ImGui.TextColored(new Vector4(0.45f, 0.72f, 1f, 1f), $"状態: {state}");
        ImGui.SameLine();
        if (ImGui.Button("ON")) { configuration.IsEnabled = true; configuration.IsAutomationEnabled = false; Save(); }
        ImGui.SameLine();
        if (ImGui.Button("AUTO")) { configuration.IsEnabled = true; configuration.IsAutomationEnabled = true; Save(); }
        ImGui.SameLine();
        if (ImGui.Button("OFF")) { configuration.IsEnabled = false; configuration.IsAutomationEnabled = false; Save(); }
    }

    private void DrawTree() {
        ImGui.Text("実行ツリー（順番どおりに子へ遷移）");
        foreach (var root in configuration.Roots) DrawTreeNode(root);
    }

    private void DrawTreeNode(StrategyNode node) {
        var selected = selectedId == node.Id;
        var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth | (selected ? ImGuiTreeNodeFlags.Selected : 0);
        if (node.Children.Count == 0) flags |= ImGuiTreeNodeFlags.Leaf;
        var open = ImGui.TreeNodeEx($"{node.Label}##{node.Id}", flags);
        if (ImGui.IsItemClicked()) selectedId = node.Id;
        if (open) { foreach (var child in node.Children) DrawTreeNode(child); ImGui.TreePop(); }
    }

    private void DrawEditorAndPreview() {
        var selected = Find(selectedId, configuration.Roots);
        ImGui.Text("設定 / プレビュー");
        if (selected == null) { ImGui.TextDisabled("左のツリーから手順を選択してください。"); return; }
        var changed = false;
        var label = selected.Label;
        if (ImGui.InputText("表示名", ref label, 100)) { selected.Label = label; changed = true; }
        var trigger = selected.TriggerText;
        if (ImGui.InputText("バトルログ・トリガー", ref trigger, 200)) { selected.TriggerText = trigger; changed = true; }
        var enabled = selected.Enabled;
        if (ImGui.Checkbox("この手順を有効化", ref enabled)) { selected.Enabled = enabled; changed = true; }
        if (boardNames.Count > 0) {
            var index = Math.Max(0, boardNames.FindIndex(x => string.Equals(x, selected.BoardName, StringComparison.CurrentCultureIgnoreCase)));
            if (ImGui.Combo("移動先図面", ref index, boardNames.ToArray(), boardNames.Count)) { selected.BoardName = boardNames[index]; changed = true; }
        } else { var boardName = selected.BoardName; if (ImGui.InputText("移動先図面", ref boardName, 100)) { selected.BoardName = boardName; changed = true; } }
        if (ImGui.Button("この図面を表示")) { if (!boards.RequestShow(selected.BoardName, out _)) { } }
        ImGui.SameLine();
        if (ImGui.Button("子手順を追加")) { selected.Children.Add(NewNode()); changed = true; }
        ImGui.SameLine();
        if (ImGui.Button("削除") && Remove(selected.Id, configuration.Roots)) { selectedId = null; changed = true; }
        if (changed) Save();

        ImGui.Separator();
        ImGui.TextColored(new Vector4(0.45f, 0.72f, 1f, 1f), "プレビュー");
        ImGui.TextWrapped($"ログに「{selected.TriggerText}」を含む行を検出すると、図面「{selected.BoardName}」を表示します。");
        if (selected.Children.Count > 0) ImGui.TextWrapped($"次は子手順 {selected.Children.Count} 件だけが候補になります。子がない場合はルートに戻ります。");
        else ImGui.TextWrapped("子手順がないため、表示後はルートの先頭から再び判定します。");
    }

    private void Save() { configuration.Save(); runner.Reset(); }
    private static StrategyNode NewNode() => new() { Label = "新しい手順", TriggerText = "ログ文言", BoardName = "図面名" };
    private static StrategyNode? Find(Guid? id, IEnumerable<StrategyNode> nodes) { foreach (var node in nodes) { if (node.Id == id) return node; var found = Find(id, node.Children); if (found != null) return found; } return null; }
    private static bool Remove(Guid id, List<StrategyNode> nodes) { if (nodes.RemoveAll(x => x.Id == id) > 0) return true; return nodes.Any(x => Remove(id, x.Children)); }
    private static void PushDarkBlueTheme() { ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.025f, 0.06f, 0.14f, 0.97f)); ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.05f, 0.20f, 0.43f, 1f)); ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.08f, 0.31f, 0.62f, 1f)); ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.05f, 0.22f, 0.48f, 1f)); ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.08f, 0.34f, 0.68f, 1f)); }
    private static void PopDarkBlueTheme() => ImGui.PopStyleColor(5);
}
