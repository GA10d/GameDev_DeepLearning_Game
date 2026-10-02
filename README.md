# 深度学习游戏

这个项目探索一种面向有高中数学基础、没有深度学习经验的玩家的学习游戏。玩家从加法、乘法和可调参数开始，逐步搭建计算图、反向传播和训练循环，最终用自己做出的部件训练一个小型神经网络。

当前已进入 Unity 开发原型阶段。`UnityGame` 使用本机 Unity 2022.3.62f1，已构建 Windows 可运行版本；P00 至 B09 的 17 关覆盖接线、模块封装、梯度规则、平均器、参数更新和真实 CPU 训练。完整关卡树为 55 节点，其余关卡在游戏内标为规划中。终章目标为玩家在本机训练自己的手写数字分类器。

试玩与构建方法见 [Unity 工程说明](UnityGame/README.md)，新版直接运行 `UnityGame/Builds/Windows-UI-0.3/LearningFoundry.exe`（原 0.1 版本保留在 `Builds/Windows`）。新版 [成品制作设计与 Steam 路线](docs/production-design.md) 明确了游戏体验、系统规格、后续章节、美术资产与发行验收标准。[美术规范与资产记录](docs/art-direction.md) 记录复古工业控制台风格和当前资产来源。

UI 0.3 根据 [五款开源游戏的源码调研](docs/open-source-game-ui-study.md) 重做为实体维修工作台：部件有不同轮廓，托盘支持拿取与预览安装，接线有跟随鼠标的线缆，旋钮直接调参，控制盒和示波器可开合与移动。见 [游戏界面规范](docs/ui-design-system.md)、[Unity 实际画面与 0.2 对照](docs/ui-art-direction-preview.html) 和 [验证记录](docs/ui-verification.md)。0.1、0.2 的构建与截图继续保留，字体授权随项目保存。

完整内容见 [深度学习游戏调研与初步设计](docs/research-and-design.md)。文档包含同类游戏分析、核心机制、学习主线、首版关卡、实现架构、试玩验证方法和原始资料链接。

关于玩家 CPU 与 GPU 上的真实训练、卷积与池化模块封装，以及手写数字识别终章，见 [本地训练与模型模块设计](docs/local-training-and-modules.md)。

最新的 [关卡树与逐关玩法](docs/level-design.md) 包含 47 个必修关卡与 8 个可选支线。[可点击关卡图](docs/level-map.html) 可在浏览器中离线打开，选择节点即可查看玩法与前置路径。[关卡数据](docs/levels.json) 是地图与玩法文档的共同数据源。

1.1 版补充了 [模块首次教学与解锁审计](docs/module-introductions.md)，覆盖 69 项玩家可见工具和模块；[逐关结果可视化](docs/visualization-design.md) 为全部 55 关规定主画面、操作联动、失败定位和通关成果。[六组交互画面示例](docs/visualization-preview.html) 可在浏览器离线运行，用实际局部算式演示画面表达。

修改关卡数据后，可用 Python 运行 [内容检查器](tools/validate-level-data.py)，核对教学前置、地图数据与逐关说明是否完整。

当前验证包含 58 项核心检查和 Windows 原生程序训练/渲染检查，见 [开发验证记录](docs/unity-verification.md)。下一阶段先扩展至 C05 的非线性分类，再依赖关卡树扩展张量、卷积、池化与 MNIST。当前没有 GPU 训练、完整 MNIST 任务或 Steam SDK 集成。

调研日期：2026 年 10 月 2 日。
