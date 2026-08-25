using AutoStrategyBoardPagination.Models;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace AutoStrategyBoardPagination;

[Serializable]
public sealed class Configuration : IPluginConfiguration {
    public int Version { get; set; } = 1;
    public bool IsEnabled { get; set; }
    public bool IsAutomationEnabled { get; set; }
    public List<StrategyNode> Roots { get; set; } = [];

    [NonSerialized] private IDalamudPluginInterface? pluginInterface;

    public void Initialize(IDalamudPluginInterface pluginInterface) => this.pluginInterface = pluginInterface;
    public void Save() => this.pluginInterface?.SavePluginConfig(this);
}
