# 深度学习游戏

这个项目探索一种面向有高中数学基础、没有深度学习经验的玩家的学习游戏。玩家从加法、乘法和可调参数开始，逐步搭建计算图、反向传播和训练循环，最终用自己做出的部件训练一个小型神经网络。

当前已进入 Unity 开发原型阶段。`UnityGame` 使用本机 Unity 2022.3.62f1，已构建 Windows 可运行版本；55关已形成首轮可玩的完整路线：原有17关可接线标量图，新增38关覆盖非线性、张量、分类、卷积、池化和实际MNIST训练。终章可画自己的数字并导出模型。详见 [55关试玩说明与实际边界](docs/campaign-playable-v05.md)。

试玩与构建方法见 [Unity 工程说明](UnityGame/README.md)，新版直接运行 `UnityGame/Builds/Windows-Campaign-0.5.0/LearningFoundry.exe`（旧构建均保留）。新版 [成品制作设计与 Steam 路线](docs/production-design.md) 明确了游戏体验、系统规格、后续章节、美术资产与发行验收标准。[美术规范与资产记录](docs/art-direction.md) 记录复古工业控制台风格和当前资产来源。

0.4.1 支持在工作台内按住右键拖动，自由平移画布。从空白、部件或旋钮上开始都可以；右键单击仍放下工具，持线或拿取零件时也能平移。控制盒和示波器留在原位，左键继续用于安装、接线和部件操作。

0.4 加入首次学习路线和 P00 四步现场指引：送入样本 → 启动训练 → 同输入对比 → 验收解锁。路线可跳过、重看，旧存档的工作台与进度保留。教程逻辑参考 Factorio 官方设计文章与 shapez 开源实现，见 [首次体验设计](docs/onboarding-design.md)。

UI 根据 [五款开源游戏的源码调研](docs/open-source-game-ui-study.md) 重做为实体维修工作台：部件有不同轮廓，托盘支持拿取与预览安装，接线有跟随鼠标的线缆，旋钮直接调参，控制盒和示波器可开合与移动。见 [游戏界面规范](docs/ui-design-system.md)、[Unity 实际画面与旧版对照](docs/ui-art-direction-preview.html) 和 [验证记录](docs/ui-verification.md)。旧构建与截图继续保留，字体授权随项目保存。

完整内容见 [深度学习游戏调研与初步设计](docs/research-and-design.md)。文档包含同类游戏分析、核心机制、学习主线、首版关卡、实现架构、试玩验证方法和原始资料链接。

关于玩家 CPU 与 GPU 上的真实训练、卷积与池化模块封装，以及手写数字识别终章，见 [本地训练与模型模块设计](docs/local-training-and-modules.md)。

最新的 [关卡树与逐关玩法](docs/level-design.md) 包含 47 个必修关卡与 8 个可选支线。[可点击关卡图](docs/level-map.html) 可在浏览器中离线打开，选择节点即可查看玩法与前置路径。[关卡数据](docs/levels.json) 是地图与玩法文档的共同数据源。

1.1 版补充了 [模块首次教学与解锁审计](docs/module-introductions.md)，覆盖 69 项玩家可见工具和模块；[逐关结果可视化](docs/visualization-design.md) 为全部 55 关规定主画面、操作联动、失败定位和通关成果。[六组交互画面示例](docs/visualization-preview.html) 可在浏览器离线运行，用实际局部算式演示画面表达。

修改关卡数据后，可用 Python 运行 [内容检查器](tools/validate-level-data.py)，核对教学前置、地图数据与逐关说明是否完整。

当前数值验证包含96项标量/关卡依赖回归、149项新增关卡与真实训练检查，并有Windows原生截图/交互验证。MNIST已接入，GPU训练与Steam SDK尚未集成。

调研日期：2026 年 10 月 2 日。
