> UI 0.3 已重做为实体维修工作台。托盘点击或数字键先拿取零件，再在空位左键安装；右键 / Esc 放回。拖动旋钮调参，Shift 微调；Tab 开合控制盒，Space 送样，F1 查阅知识。控制盒与示波器可拖动。详细规范见 `../docs/ui-design-system.md`，旧构建继续保留。

# Learning Foundry / 学习工坊 — Unity 原型 0.3.0

UI 0.3 改版：新版试玩位于 `Builds/Windows-UI-0.3/LearningFoundry.exe`；配色、分层、章节、读数和手册已重组。旧版 `Builds/Windows` 保留。详见 `../docs/ui-design-system.md`、`../docs/ui-verification.md` 和可离线打开的 `../docs/ui-art-direction-preview.html`。

真实 Unity Windows 游戏工程。美术方向：复古工业控制台、实体计算模块、发光线路、仪表和维修工坊。当前可玩范围为 P00、A01–A07、B01–B09，共 17 关；完整 55 节点关卡树中的后续节点标为规划中。

## 直接试玩

运行 `Builds/Windows-UI-0.3/LearningFoundry.exe`，保留同目录的 `LearningFoundry_Data`、`UnityPlayer.dll` 和 `MonoBleedingEdge` 等文件。只有 exe 不能独立运行。

不需要玩家安装 Unity、Python、CUDA 或深度学习框架。当前数学引擎在本机 CPU 上计算；GPU 用于 Unity 画面渲染，GPU **训练**尚未实现。

1. 主菜单进入第一份委托。P00 先送入样本，观察训练前结果，然后启动训练，暂停后再送入同样样本，点击验收。
2. 点模块右侧插孔选择线路源，再点目标左侧插孔接线。点模块可查看参数；拖动实体面板移动。拖动工作台空白区域平移，滚轮滚动，工作垫右下角 `− / + / 全图` 调整缩放。
3. 送入样本查看当前线路的实际读数。验收会运行多组校准数据和新输入；成功后解锁所有前置已完成的下一关。
4. `Ctrl+Z / Ctrl+Y` 撤销、重做；Esc 取消接线并暂停。单关重置有游戏内确认，并可撤销。
5. A06 将 A04 的整张单输出工作台封装为盒子，并放置独立实例。A07 先搭四读数平均器，再组合模型与误差计。B09 使用已经保存的反向规则、平均器和更新器实际训练，可暂停、单步和保存/恢复模型快照。

存档位置：`%USERPROFILE%\AppData\LocalLow\DeepLearningWorkshop\Learning Foundry\profile-v1.json`。工作台、完成记录、自建模块、规则与模型快照保存在这里。当前随项目分发思源黑体与 IBM Plex Mono 原版字体，并保留完整 OFL 许可；没有分发 Windows 系统字体。

## 在 Unity 中开发

本机已验证的编辑器：`F:\Unity\Installs\2022.3.62f1\Editor\Unity.exe`。使用 Unity Hub 打开本目录，打开 `Assets/Scenes/Workshop.unity` 后运行。界面由 `FoundryGame` 在运行时创建，因此编辑状态下场景不会显示完整 UI。菜单 `Learning Foundry / Prepare Project` 创建/配置场景，`Build Windows` 导出游戏。

CLI 在 `C:\Users\32881\AppData\Local\Unity\bin\unity.exe`；PowerShell 示例：

```powershell
$taskProject = 'F:\Documents\GitHub\GameDev_DeepLearning_Game\UnityGame'
$taskEditor = 'F:\Unity\Installs\2022.3.62f1\Editor\Unity.exe'
$taskCli = 'C:\Users\32881\AppData\Local\Unity\bin\unity.exe'

& $taskCli run $taskProject --editor-path $taskEditor --timeout 300 -- -nographics -executeMethod LearningFoundry.Editor.BuildAutomation.Prepare
& $taskCli run $taskProject --editor-path $taskEditor --timeout 300 -- -nographics -executeMethod LearningFoundry.Editor.CoreVerification.Run
& $taskCli build $taskProject --target StandaloneWindows64 --editor-path $taskEditor --execute-method LearningFoundry.Editor.BuildAutomation.BuildWindows --output-path "$taskProject\Builds\Windows-UI-0.3\LearningFoundry.exe" --no-tail --allow-dirty-build --timeout 600
```

`unity run` 自行管理 batchmode/quit，不要在 `--` 后再加 `-batchmode` 或 `-quit`。这些例子使用本机已有编辑器，不安装或升级 Unity。

## 代码与内容

| 文件 | 职责 |
|---|---|
| `Assets/Scripts/Core/GraphEngine.cs` | 可接线标量计算图、运算带、梯度累加、独立模块参数、训练阶段执行 |
| `Assets/Scripts/Core/Campaign.cs` | 关卡数据、存档、任务初始设备、数值验收、参考演示设备 |
| `Assets/Scripts/UI/HardwareUI.cs` | 原创木台、金属皮肤、部件轮廓、插孔、旋钮和窗口拖动 |
| `Assets/Scripts/UI/WorkshopUI.cs` | 面板、插孔、拖动、发光线路、实际数值与损失曲线 |
| `Assets/Scripts/Game/FoundryGame.cs` | 主菜单、55 节点关卡树、17 关玩法、训练、撤销、快照、原生截图验证 |
| `Assets/Resources/Campaign/levels.json` | 从根目录 `docs/levels.json` 同步的教学与玩法数据 |
| `Assets/Editor/CoreVerification.cs` | 数学、关卡判定、错误输入和真实训练验证；不随玩家构建分发 |
| `Assets/Editor/BuildAutomation.cs` | 场景准备与 Windows 构建 |

运行时不调用聊天模型或生成图像服务。首版 CPU 引擎采用 double 标量；后续 Dense、Conv、Pool 等张量模块需要扩展数值后端与形状检查，不能仅增加一个节点图标。

`--smoke --capture-output <目录>` 是原生程序的测试入口：使用隔离的内存存档，生成主菜单、关卡图、训练工作台实际 Unity 渲染图和训练结果报告，随后退出。它不会修改玩家存档。隐藏窗口截图通过实际 Unity 摄像机渲染到纹理，以免最小化窗口的屏幕缓冲为黑色。

## 当前边界

这是已构建且验证的开发原型，还不是 Steam 上架成品。当前封装操作针对整张 A04 工作台；尚无框选子图、多输出封装或任意盒子编辑。链式法则使用切换回流顺序的专门实验，阶段循环使用卡片排序。还需补充完整章节、动画与音频资产、跨分辨率人工试玩、模型/关卡编辑工具、国际化字体、Steam SDK 与发行 QA。详细路线见根目录 `docs/production-design.md`。
