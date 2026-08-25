using AutoStrategyBoardPagination.Services;
using AutoStrategyBoardPagination.Windows;
using Dalamud.Game.Chat;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;

namespace AutoStrategyBoardPagination;

public sealed class Plugin : IDalamudPlugin {
    public string Name => "AutoStrategyBoardPagination";
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private readonly Configuration configuration;
    private readonly BoardService boards;
    private readonly TreeRunner runner;
    private readonly WindowSystem windows = new("AutoStrategyBoardPagination");
    private readonly ConfigWindow configWindow;

    public Plugin() {
        configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        configuration.Initialize(PluginInterface);
        boards = new BoardService(Framework, Log);
        runner = new TreeRunner(configuration, boards, text => ChatGui.Print(text, "ASBP"), text => ChatGui.PrintError(text, "ASBP"));
        configWindow = new ConfigWindow(configuration, boards, runner);
        windows.AddWindow(configWindow);
        CommandManager.AddHandler("/asbp", new CommandInfo(OnCommand) { HelpMessage = "/asbp on | auto | off | 図面名（設定画面は引数なし）" });
        ChatGui.ChatMessage += OnChatMessage;
        PluginInterface.UiBuilder.Draw += windows.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfig;
    }

    private void OnChatMessage(IHandleableChatMessage message) {
        if (message.LogKind is XivChatType.Damage or XivChatType.Miss or XivChatType.Action or XivChatType.Item or XivChatType.Healing or XivChatType.GainBuff or XivChatType.GainDebuff or XivChatType.LoseBuff or XivChatType.LoseDebuff)
            runner.OnBattleLog(message.Message.TextValue);
    }

    private void OnCommand(string _, string arguments) {
        var value = arguments.Trim();
        if (value.Length == 0) { ToggleConfig(); return; }
        if (value.Equals("on", StringComparison.OrdinalIgnoreCase)) { configuration.IsEnabled = true; configuration.IsAutomationEnabled = false; configuration.Save(); ChatGui.Print("有効化しました（自動遷移は停止中）。", "ASBP"); return; }
        if (value.Equals("auto", StringComparison.OrdinalIgnoreCase)) { configuration.IsEnabled = true; configuration.IsAutomationEnabled = true; runner.Reset(); configuration.Save(); ChatGui.Print("自動遷移を有効化しました。", "ASBP"); return; }
        if (value.Equals("off", StringComparison.OrdinalIgnoreCase)) { configuration.IsEnabled = false; configuration.IsAutomationEnabled = false; configuration.Save(); ChatGui.Print("無効化しました。", "ASBP"); return; }
        if (boards.RequestShow(value, out var error)) ChatGui.Print($"図面「{value}」を表示します。", "ASBP"); else ChatGui.PrintError(error, "ASBP");
    }

    private void ToggleConfig() => configWindow.Toggle();
    public void Dispose() { ChatGui.ChatMessage -= OnChatMessage; CommandManager.RemoveHandler("/asbp"); PluginInterface.UiBuilder.Draw -= windows.Draw; PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfig; windows.RemoveAllWindows(); boards.Dispose(); }
}
