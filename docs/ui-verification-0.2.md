# UI 0.2 实装与验证

日期：2026-10-02。编辑器 Unity 2022.3.62f1；Windows 64 位独立程序。

## 已落实的改版

- 石墨设备框、纸页、工程板、克制的橙色主操作；去掉编辑页面的大幅插画与泛用发光圆角。
- 顶部规格、左侧零件、中央线路、右侧对象 / 实验设置、底部测量记录。
- 读数区可开合，不重建或重排当前计算图；选择对象不销毁测量记录。
- 章节筛选、全树总览、选中委托的知识点与前置详情；规划关卡不可进入。
- 独立技术手册，保留各关的新知识、动作、规格与提示。当前是模态纸页，后续可做非模态抽屉。
- 失败内嵌反馈，避免每次验收都弹出窗口；Esc 关闭当前浮层，浮层中不触发底层撤销 / F1。
- 思源黑体与 Plex Mono 随项目保存；数字小于 0.0001 时使用科学计数法，避免误显示为零。
- 端口视觉与 28 px 基准命中范围分开；封装模块显示真实接口名；长内容具有滚动条。

## 重新执行的原生程序检查

`verification/ui-0.2/player-smoke.json` 是最终运行结果，所有下列条件通过：

| 检查 | 结果 |
| --- | --- |
| P00 实际训练、观察与通关后进入 A01 | 通过 |
| A01 调用实际源 / 目标插孔回调接线并通关 | 通过 |
| A02 验收失败留在工作台，不生成模态窗口 | 通过 |
| 选择参数时保留下方读数 | 通过 |
| 开合读数区保留画布位置 | 通过 |
| 打开 / 关闭手册保留位置和读数 | 通过 |
| B09 执行 180 个训练步骤并通过数值规格 | 通过；loss = 7.7477170214724425e-9 |

七张原生截图为 1600×900：首页、章节、训练工作台、首关接线、失败、对象属性、手册。画面由实际 Unity Camera 渲染到纹理后保存，未使用静态 HTML 冒充游戏。自动测试使用内存中的隔离进度，不写入玩家存档。

计算核心 `GraphEngine.cs`、关卡计算与保存模型 `Campaign.cs` 未在本次修改。此前 58 项核心检查保留在 `core-verification.json`；本次重新验证的是 UI 操作流程及其调用的真实训练，未声称重跑所有旧检查。

## HTML 设计档案检查

`preview-verification.json`：七个原生画面资源加载、新旧对比、区域标注、通道选择、Conv / ReLU / Pool 形状、8 个通道、池化热图尺寸、像素读数、字体、无脚本错误全部通过。684 px 宽的设计档案不出现横向溢出；这不是 Unity 720p 布局验收。

张量设计页使用代码绘制输入和固定滤波器，真实计算 28×28 → 26×26 → 13×13；没有训练过程或 MNIST 数据。池化检查器显示当前窗口四个值，ReLU 显示门前 / 门后，热图通道共享同一层标尺。

颜色核对仅覆盖设计 token 的常见正文配对，Paper 上 Ink / Muted / Accent / Signal / Fault 均 ≥4.5:1，详见 `color-contrast.json`。没有据此宣称整个游戏已经满足完整可访问性标准。

## 仍需制作的产品能力

可调整停靠布局、非模态手册、结构化样本表及点击定位、连线预览、自动避让、缩放后端口的最小命中范围、完整键盘与字号设置、原生多分辨率验收、训练 / 验证双曲线、张量 / CNN 与 GPU 后端。用户测试尚未进行。

当前改版是可运行的 0.2 UI 原型，游戏内容仍为 17 关；不应作为 Steam 发售完成度的证明。

## 运行与复现

运行 `UnityGame/Builds/Windows-UI-0.2/LearningFoundry.exe`。直接打开 `docs/ui-art-direction-preview.html` 可离线查看设计档案、新旧对比与张量试验。

原生自动验证：在 PowerShell 中运行以下命令，测试结束后自动退出；截图与 JSON 放到指定文件夹。

```powershell
& .\UnityGame\Builds\Windows-UI-0.2\LearningFoundry.exe --smoke --capture-output "$PWD\verification\ui-test" -logFile "$PWD\verification\ui-test.log"
```

构建可使用本机 Unity CLI：

```powershell
unity build .\UnityGame --target StandaloneWindows64 --editor-path 'F:\Unity\Installs\2022.3.62f1\Editor\Unity.exe' --execute-method LearningFoundry.Editor.BuildAutomation.BuildWindows --output-path "$PWD\UnityGame\Builds\Windows-UI-0.2\LearningFoundry.exe" --no-tail --allow-dirty-build --timeout 600
```

原来的 `Builds/Windows` 和 0.1 截图保留，可对比与回退。
