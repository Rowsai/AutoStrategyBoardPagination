namespace AutoStrategyBoardPagination.Models;

/// <summary>One step in the saved, ordered trigger tree.</summary>
public sealed class StrategyNode {
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Label { get; set; } = "新しい手順";
    public string TriggerText { get; set; } = string.Empty;
    public string BoardName { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public List<StrategyNode> Children { get; set; } = [];
}
