using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace AutoStrategyBoardPagination.Services;

/// <summary>Bridges the supported FFXIVClientStructs Tofu data to the native preview action.</summary>
public unsafe sealed class BoardService : IDisposable {
    private readonly IFramework framework;
    private readonly IPluginLog log;
    private PendingSelection? pending;

    public BoardService(IFramework framework, IPluginLog log) {
        this.framework = framework;
        this.log = log;
        this.framework.Update += OnFrameworkUpdate;
    }

    public IReadOnlyList<string> GetSavedBoardNames() {
        var module = TofuModule.Instance();
        if (module == null) return [];

        var names = new List<string>();
        foreach (var board in module->SavedBoardData->Boards) {
            if (board.IsValid && !string.IsNullOrWhiteSpace(board.NameString)) names.Add(board.NameString);
        }
        return names.OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public bool RequestShow(string boardName, out string error) {
        if (string.IsNullOrWhiteSpace(boardName)) { error = "図面名が空です。"; return false; }
        var module = TofuModule.Instance();
        if (module == null) { error = "Strategy Board のデータを取得できません。ログイン後に試してください。"; return false; }

        var uiIndex = -1;
        for (uint i = 0; i < module->SavedBoardData->MaxCount; i++) {
            var board = module->GetBoardAtUIIndex(TofuType.Saved, i);
            if (board != null && board->IsValid && string.Equals(board->NameString, boardName, StringComparison.CurrentCultureIgnoreCase)) { uiIndex = (int)i; break; }
        }
        if (uiIndex < 0) { error = $"保存済み図面「{boardName}」が見つかりません。"; return false; }

        var agentModule = AgentModule.Instance();
        var baseAgent = agentModule == null ? null : agentModule->GetAgentByInternalId(AgentId.TofuList);
        if (baseAgent == null || !baseAgent->IsActivatable()) { error = "Strategy Board を現在開けません。"; return false; }
        // Do not call Show while a preview is open: the game treats it as a toggle and
        // only closes the current board. The framework update closes the preview first.
        pending = new PendingSelection(uiIndex, boardName, 0, 0, PendingStage.ClosePreview);
        error = string.Empty;
        return true;
    }

    private void OnFrameworkUpdate(IFramework _) {
        if (pending is not { } request) return;
        var agentModule = AgentModule.Instance();
        if (agentModule == null) {
            AdvanceOrCancel(request);
            return;
        }

        if (request.Stage == PendingStage.ClosePreview) {
            var preview = agentModule->GetAgentByInternalId(AgentId.TofuPreview);
            if (preview != null && preview->IsAgentActive()) {
                preview->Hide();
                pending = request with { Frames = request.Frames + 1, Stage = PendingStage.WaitForPreviewClose };
                return;
            }
            pending = request with { Frames = request.Frames + 1, Stage = PendingStage.OpenList };
            return;
        }

        if (request.Stage == PendingStage.WaitForPreviewClose) {
            var preview = agentModule->GetAgentByInternalId(AgentId.TofuPreview);
            if (preview != null && preview->IsAgentActive()) { AdvanceOrCancel(request); return; }
            pending = request with { Frames = request.Frames + 1, Stage = PendingStage.OpenList };
            return;
        }

        var agent = (AgentTofuList*)agentModule->GetAgentByInternalId(AgentId.TofuList);
        if (request.Stage == PendingStage.OpenList) {
            if (agent == null || !agent->IsActivatable()) { AdvanceOrCancel(request); return; }
            if (!agent->IsAgentActive()) agent->Show();
            pending = request with { Frames = request.Frames + 1, Stage = PendingStage.WaitForRows };
            return;
        }

        if (agent != null && agent->Data != null) {
            var data = agent->Data;
            if (data->TotalSavedList > (uint)request.UiIndex) {
                // Give the addon a few frames after the rows become available so the native
                // selection state and the visible list are synchronized.
                if (request.ReadyFrames >= 3) {
                    data->IsSharedListOpen = 0;
                    data->SavedSelectedIndex = request.UiIndex;
                    data->ReviewSelectedBoard();
                    log.Debug("Opening Strategy Board '{BoardName}' in review mode (list index {Index}).", request.BoardName, request.UiIndex);
                    pending = null;
                    return;
                }
                pending = request with { Frames = request.Frames + 1, ReadyFrames = request.ReadyFrames + 1 };
                return;
            }
        }
        AdvanceOrCancel(request);
    }

    private void AdvanceOrCancel(PendingSelection request) {
        if (request.Frames >= 120) {
            log.Warning("Strategy Board preview did not become ready for {BoardName}.", request.BoardName);
            pending = null;
        } else pending = request with { Frames = request.Frames + 1 };
    }

    public void Dispose() => framework.Update -= OnFrameworkUpdate;
    private readonly record struct PendingSelection(int UiIndex, string BoardName, int Frames, int ReadyFrames, PendingStage Stage);
    private enum PendingStage { ClosePreview, WaitForPreviewClose, OpenList, WaitForRows }
}
