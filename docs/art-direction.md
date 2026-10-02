> 当前 UI 已升级为 Unity 0.3 的实体维修工作台。本文保留早期制作记录；最新界面、美术载体和资产边界见 [游戏界面规范](ui-design-system.md) 与 [开源游戏源码调研](open-source-game-ui-study.md)。

# 学习工坊：美术规范与资产记录

> 本页记录 0.1 的初始美术方向。当前 UI 的配色、布局、字体与组件规范以 [UI 0.2 设计体系](ui-design-system.md) 为准；原始工坊插画保留为素材档案。

2026-10-02 · 已确认方向：复古工业控制台，实体模块、发光线路、仪表和维修工坊。

## 视觉目标

让玩家感觉自己在修一台有重量、有历史的机器。背景建立维修工坊的空间与气氛；实际交互集中在可读的控制台上。钢板、黄铜和米色铭牌形成材质区别，少量青色信号与暖色指示灯表达工作状态。磨损保留在边缘，文字、端口和核心图形周围保持清楚。

固定正面视角，装配板使用 2D UI。实体感来自面板边框、顶边材质条、插孔、铆钉、读数和交互反馈。美术必须接受任意节点数量、任意连线和不同语言长度，不能把关键端口或公式烘焙进背景图片。

当前 Unity 原生主菜单和工作台截图：

![Unity 主菜单](../verification/screenshots/01-main-menu.png)

![Unity 训练工作台](../verification/screenshots/03-workshop.png)

这些是实际原生程序渲染，工作台数据由真实计算更新。训练截图使用自动测试夹具展示已搭好的设备，并不是声称已经由新玩家完成全关卡试玩。

## 颜色与形状语言

| 用途 | 当前色值 | 使用原则 |
|---|---|---|
| 深色底板 | `#0A1113` | 工作台与仪表底，不与亮线竞争 |
| 面板 | `#111B1D` | 保持背景暗而有层次 |
| 钢板边框 | `#1F3336` | 零件和按钮实体边缘 |
| 文字/铭牌 | `#E0DBC2` | 暖白文字，统一读数层级 |
| 正向信号 | `#52E6D1` | 发光连线与活跃读数 |
| 参数/回流/解锁 | `#D99C4D` | 黄铜旋钮、梯度强调与新委托 |
| 错误 | `#F56B57` | 仅标具体错误位置，并附文字或图标 |

输入/输出面板带 IN/OUT 与端口名；参数使用旋钮标记；运算用 +、×；封装盒子有名字和可展开标识。后续张量端口增加形状铭牌，数据类型以形状/标记区分，避免只靠颜色。

默认中文正文建议在 1600×900 参考尺寸至少 18px，核心操作 18–21px。全图缩放可以隐藏或减小次要读数，但点击和放大后须可读。正式版增加大字号布局与独立 UI 缩放，不将整个屏幕同比缩小作为唯一适配手段。

## 动态反馈

插孔在可连接时亮起，连接成功伴随轻短机械声；错误连接保留预览并标原因。运行后，模块显示实际中间值；目前指示灯按运算带的依赖顺序示意播放，回流视图按逆序播放。它是记录的可视化，不能被理解为 GPU 硬件正在逐个灯执行。

后续增加沿真实路径的移动信号、可暂停的播放时间轴、参数更新前后对照、正确/错误样本的出口动画。一次运行涉及的所有结果取自同一计算记录。减少动画设置保留完整读数和验收反馈。

曲线使用真实采样值，明确横轴为训练步/测量序号，纵轴为损失与范围。比较多个模型必须使用统一刻度；自动缩放有标识。热图可选择共同范围，负值/正值采用明确的双色标尺。花哨特效不能遮住训练是否实际改善。

## 资产制作批次

| 批次 | 所需资产 | 使用目的 |
|---|---|---|
| 当前原型 | 工坊背景、程序化面板/端口/电缆/仪表、原创短点击音 | 已进入 Unity 游戏 |
| Demo | 控制台板面、旋钮刻度、模块类别图标、样本卡/传送带、完整 UI 音效组 | 打磨 P–C，提升解谜操作反馈 |
| 主线 | 各章节的设备外壳、数据检验封印、视觉工坊窗口、档案柜与手写板 | 体现宏观设备逐步搭建 |
| 发行 | 标志、Steam 商店与库图、实机预告、成就图标、授权字体 | 从实际产品统一提炼，避免使用概念图冒充实机截图 |

图标与简单 UI 形状继续采用可编辑代码/矢量系统。需要绘画感的场景、材质、设备插画再制作位图。量产前统一构图、材质和视角，不从不同风格素材包随机拼接。

## 当前资产来源

| 资产 | 路径 | 来源与分发记录 |
|---|---|---|
| 工坊背景 | `UnityGame/Assets/Resources/Art/workshop.png` | imagegen 内置工具，本轮新生成；已复制进用户 F 盘工程。作为随游戏分发的预生成 AI 美术记录，发行前填写对应内容调查 |
| 模块、铆钉、插孔、发光线、曲线 | `UnityGame/Assets/Scripts/UI/WorkshopUI.cs` | 项目内程序化 UI，支持任意图结构 |
| 继电器点击音 | 同上 `Sound.Click` | 项目代码生成正弦衰减波形，不引用第三方音频 |
| 中文字体 | 运行时系统动态字体 | 当前优先 Microsoft YaHei；没有复制或分发 Windows 字体。成品需导入允许商用分发的 CJK 字体并随包提供许可文本 |

工程未添加来源不明的商店素材、音乐或游戏截图。当前背景仅用于本项目，不是 Turing Complete 的资产或界面复制。

## 生成记录

模式：`image_gen.imagegen` 内置工具，单张全新生成，非透明背景。原始输出留在工具默认目录，工程消费文件已复制到 `F:\Documents\GitHub\GameDev_DeepLearning_Game\UnityGame\Assets\Resources\Art\workshop.png`，交付镜像保存在 `outputs/deep-learning-game` 下同一路径。

实际提示词：

> Create a polished 16:9 background illustration asset for a commercial-style indie PC puzzle game called Learning Foundry. Art direction: retro industrial electronics repair workshop, tangible analog instruments, warm brass, aged dark blue-green painted steel, cream enamel panels, glowing cyan signal wires and small amber indicator lights, wooden bench, coils and scientific machinery. Stylized carefully crafted game environment, painterly texture with precise coherent perspective and appealing shapes, not photorealistic. Straight-on workstation viewpoint, broad clean dark uncluttered empty work area across the central 65 percent so an interactive node editor and UI can be overlaid later. Rich machinery and shelving concentrated along far left/right edges and top, workshop depth in background, soft warm overhead desk lamp contrasted with teal glow, inviting curious engineering mood. No characters, no text, no logos, no lettering, no baked-in UI, no graphs or mathematical formulas. High quality readable environment, restrained texture, subtle wear, dark overall values suitable as gameplay background.

已检查无烘焙文本、无角色和品牌，主菜单中心留白可用；实际 Unity 控制台以独立 UI 覆盖。单张背景不能替代正式字体、所有模块图标、动画和音效量产。
