using AutoStrategyBoardPagination.Models;

namespace AutoStrategyBoardPagination.Services;

/// <summary>Executes one matching tree node, then makes only its children eligible.</summary>
public sealed class TreeRunner {
    private readonly Configuration configuration;
    private readonly BoardService boards;
    private readonly Action<string> info;
    private readonly Action<string> error;
    private IReadOnlyList<StrategyNode>? candidates;

    public TreeRunner(Configuration configuration, BoardService boards, Action<string> info, Action<string> error) {
        this.configuration = configuration;
        this.boards = boards;
        this.info = info;
        this.error = error;
        Reset();
    }

    public void Reset() => candidates = configuration.Roots;

    public void OnBattleLog(string message) {
        if (!configuration.IsEnabled || !configuration.IsAutomationEnabled) return;
        var match = (candidates ?? configuration.Roots).FirstOrDefault(node => node.Enabled && !string.IsNullOrWhiteSpace(node.TriggerText) && message.Contains(node.TriggerText, StringComparison.OrdinalIgnoreCase));
        if (match == null) return;
        if (!boards.RequestShow(match.BoardName, out var reason)) { error(reason); return; }
        candidates = match.Children.Count == 0 ? configuration.Roots : match.Children;
        info($"トリガー「{match.TriggerText}」: {match.BoardName}");
    }
}
