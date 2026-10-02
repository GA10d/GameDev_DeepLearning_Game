# 从开源游戏源码重新设计学习工坊的 UI

调研与实现日期：2026-10-02。对应可运行版本：Unity 0.3.0。

上一版的问题不只在配色。工作台被组织为“左侧功能列表、中央编辑器、右侧属性表、下方数据面板”，所有部件也使用几乎相同的矩形卡片。这套结构方便展示功能，却削弱了玩家拿取、安装、接线和修理一台机器的感觉。本轮直接阅读官方游戏仓库的界面代码，提炼交互组织方法，再在现有 Unity 游戏中独立实现。

## 实际查看的五个项目

| 项目 | 与本游戏的关系 | 重点阅读 |
|---|---|---|
| [Mindustry](https://github.com/Anuken/Mindustry) | 自动化、建造、部件选择 | `Styles.java`、`HudFragment.java`、`PlacementFragment.java` |
| [shapez 第一代](https://github.com/tobspr-games/shapez.io) | 最接近“选部件、搭系统、观察运行”的开源样本 | `hud.js`、`base_toolbar.js`、`building_placer.js`、建造工具栏样式 |
| [OpenTTD](https://github.com/OpenTTD/OpenTTD) | 在持续运行的游戏世界上组织密集工具和窗口 | `toolbar_gui.cpp`、`widget.cpp`、`window.cpp` |
| [OpenRCT2](https://github.com/OpenRCT2/OpenRCT2) | RollerCoaster Tycoon 2 的开源重新实现；实体按钮与工具状态 | `interface/Widget.cpp`、`windows/TopToolbar.cpp` |
| [OpenRA](https://github.com/OpenRA/OpenRA) | Red Alert 等游戏的开源引擎；C# 界面与输入分层 | `ingame-player.yaml`、`ChromeProvider.cs`、`WorldInteractionControllerWidget.cs` |

这五个项目的适用范围不同。shapez、Mindustry 的建造操作最值得吸收；OpenTTD、OpenRCT2 的密度和传统窗口并不需要整套照搬；OpenRA 最适合看皮肤、布局和游戏输入如何分离。OpenRCT2、OpenRA 的开源引擎也不代表原商业游戏的数据和美术可以随意取用。

使用固定提交进行阅读，而不是只凭截图推测。代码文件的原始 URL、SHA-256 和字节数保存在 `design-assets/research/open-source-ui-source-manifest.json`。

| 仓库 | 阅读提交 |
|---|---|
| Mindustry | `69ed90bb9d130c5f8d06046e940cb7062992a0b7` |
| shapez | `8a79de5255fb6756f3a1d26579b1e9bc36e0c811` |
| OpenTTD | `f412dc618bc770e535b20d42ed891462be18ef1a` |
| OpenRCT2 | `11513222890717e81431c83a28aafb7555f2ccd2` |
| OpenRA | `7d57605bca2cbe963068d42505e00072afe19868` |

## 1. 界面围绕游戏状态组织

Mindustry 的 `PlacementFragment.build()` 把建造工具放在右下方，跟随 HUD 的可见状态；部件直接使用 `block.uiIcon`，按钮的选中状态对应 `control.input.block`，并逐帧更新能否建造。细节区域根据当前选中或悬停对象重建。这里的核心是“玩家现在拿着什么、能建什么”，而不是一份始终铺开的功能目录。证据：[工具位置与可见条件，277–280 行](https://github.com/Anuken/Mindustry/blob/69ed90bb9d130c5f8d06046e940cb7062992a0b7/core/src/mindustry/ui/fragments/PlacementFragment.java#L277-L280)、[部件图标与建造状态，294–328 行](https://github.com/Anuken/Mindustry/blob/69ed90bb9d130c5f8d06046e940cb7062992a0b7/core/src/mindustry/ui/fragments/PlacementFragment.java#L294-L328)、[对象细节更新，359–386 行](https://github.com/Anuken/Mindustry/blob/69ed90bb9d130c5f8d06046e940cb7062992a0b7/core/src/mindustry/ui/fragments/PlacementFragment.java#L359-L386)。

学习工坊的应用：底部托盘显示本关可用部件；参考设备关显示实验提示；控制盒只在需要时打开。知识点仍保留在铭牌，完整介绍与验收条件保留在 F1 手册，避免为了减少界面而移除教学。

## 2. 拿取与安装是两个不同状态

shapez 的工具栏为部件保存 `unlocked` 和 `selected` 状态，通过信号更新当前拿取的部件，同时绑定快捷键。放置逻辑把鼠标从屏幕空间转换到世界空间，生成预览实体，检查能否安装，绘制不同颜色的边界和部件预览，并显示匹配的输入、输出。选择工具不是立即向地图插入一个对象。证据：[图标、快捷键与状态，97–154 行](https://github.com/tobspr-games/shapez.io/blob/8a79de5255fb6756f3a1d26579b1e9bc36e0c811/src/js/game/hud/parts/base_toolbar.js#L97-L154)、[选择状态，229–242 行](https://github.com/tobspr-games/shapez.io/blob/8a79de5255fb6756f3a1d26579b1e9bc36e0c811/src/js/game/hud/parts/base_toolbar.js#L229-L242)、[世界坐标，293–303 行](https://github.com/tobspr-games/shapez.io/blob/8a79de5255fb6756f3a1d26579b1e9bc36e0c811/src/js/game/hud/parts/building_placer.js#L293-L303)、[合法性与预览，350–396 行](https://github.com/tobspr-games/shapez.io/blob/8a79de5255fb6756f3a1d26579b1e9bc36e0c811/src/js/game/hud/parts/building_placer.js#L350-L396)。

学习工坊的应用：点击托盘或数字键拿起零件，鼠标旁出现预览；左键在空位安装；重叠位置变红；右键或 Esc 放回。取消不会修改计算图或创建存档对象。安装和移动使用 16 个逻辑单位的网格。

这里也纠正一个容易误判的原因：shapez 的工具栏确实用 DOM 与 CSS，放置预览在游戏画布中绘制。它说明游戏感取决于操作、场景和状态组织，不能简单归因于“是不是网页技术”。

## 3. 皮肤负责材质和状态，布局负责信息

OpenRA 的 `ChromeProvider` 把面板拆为四角、四边和中心，缓存精灵，并支持不规则窗口的手动定义；`ingame-player.yaml` 则定义位置、尺寸、图像集合、快捷键与提示。美术皮肤和功能布局有各自的职责。证据：[九块面板与特殊窗口，220–259 行](https://github.com/OpenRA/OpenRA/blob/7d57605bca2cbe963068d42505e00072afe19868/OpenRA.Game/Graphics/ChromeProvider.cs#L220-L259)、[命令栏的布局、图像与输入，43–72 行](https://github.com/OpenRA/OpenRA/blob/7d57605bca2cbe963068d42505e00072afe19868/mods/ra/chrome/ingame-player.yaml#L43-L72)。

Mindustry 同样为按钮分别指定正常、按下、悬停和不可用的 drawable，并使用 NinePatch 处理可伸缩的边框。证据：[按钮状态，119–140 行](https://github.com/Anuken/Mindustry/blob/69ed90bb9d130c5f8d06046e940cb7062992a0b7/core/src/mindustry/ui/Styles.java#L119-L140)、[NinePatch，479–495 行](https://github.com/Anuken/Mindustry/blob/69ed90bb9d130c5f8d06046e940cb7062992a0b7/core/src/mindustry/ui/Styles.java#L479-L495)。

学习工坊的应用：新增原创 `HardwareUI.cs`。木台、工作垫、金属倒角、螺丝、插孔、旋钮和部件外壳由程序绘制，按钮拥有凸起与按下两种九切片皮肤。输入、参数、运算器、模型盒、输出仪表有不同轮廓和内部构造。不是仅给同一张卡片换颜色。

## 4. 状态反馈应落在工具本身

OpenRCT2 的按钮绘制直接根据“按钮已按下”或“这个工具正在使用”选择凹入/凸出的边框；随后绘制按钮图像。工具的持续状态会留在它的外观上，不需要另加一张状态说明卡。证据：[工具状态与实体边框，213–222 行](https://github.com/OpenRCT2/OpenRCT2/blob/11513222890717e81431c83a28aafb7555f2ccd2/src/openrct2-ui/interface/Widget.cpp#L213-L222)。

学习工坊的应用：拿起接线头后出现跟随鼠标的线缆，输入插孔变亮，选中的实体有边缘提示；参数的旋钮角度与真实数值对应，显示器持续呈现计算值。曲线只绘制实际采样或训练记录。发光灯的节奏是基于已执行计算顺序的提示，不冒充 GPU 执行过程。

尚待完善：托盘的持续选中皮肤、具体不兼容插孔的单独标识、多种提示音、验收报告点选定位。当前只是放置重叠检查，接线计算错误仍由实际求值和验收反馈。

## 5. 游戏世界、工具和窗口各自接收输入

OpenRA 在世界交互控制器里处理鼠标：先判断当前命令生成器，再决定执行命令、框选或让出鼠标焦点。shapez 的 HUD 统一查询是否有阻塞覆盖层、是否暂停游戏和渲染，各 UI 部件仍有独立更新与关闭生命周期。OpenTTD 的主工具栏则以精灵和提示定义工具按钮，并将主工具栏声明为不获取焦点、不可关闭的窗口。证据：[OpenRA 输入路由，85–110 行](https://github.com/OpenRA/OpenRA/blob/7d57605bca2cbe963068d42505e00072afe19868/OpenRA.Mods.Common/Widgets/WorldInteractionControllerWidget.cs#L85-L110)、[shapez 覆盖层管理，116–184 行](https://github.com/tobspr-games/shapez.io/blob/8a79de5255fb6756f3a1d26579b1e9bc36e0c811/src/js/game/hud/hud.js#L116-L184)、[OpenTTD 工具栏，2241–2274 行](https://github.com/OpenTTD/OpenTTD/blob/f412dc618bc770e535b20d42ed891462be18ef1a/src/toolbar_gui.cpp#L2241-L2274)。

学习工坊的应用：空白工作垫接收安装操作；插孔处理接线；旋钮处理拖动调参；实体外壳处理移动；控制盒和示波器的把手处理窗口移动。手册覆盖层和输入框获得焦点时，不触发画布快捷键。开合仪表不重新布局工作台，参数和测量读数保持不变。

## 0.3 的界面分层

| 层 | 玩家看到的载体 | 何时出现 | 交互职责 |
|---|---|---|---|
| 工坊环境 | 木台、金属工作垫框、设备铭牌 | 始终 | 建立空间与材质；铭牌显示本关知识点 |
| 装配现场 | 真实模块、插孔、线缆、数字显示 | 本关工作台 | 安装、移动、接线、旋钮调参 |
| 手边工具 | 下方零件托盘和控制台 | 本关工作台 | 拿取、送样、验收、撤销、开合工具 |
| 临时器材 | 可拖动的控制盒、示波器 | 实验、选中、测量或主动打开 | 精确调参、训练控制、真实曲线和读数 |
| 教学与委托 | 规格纸条、夹板手册、路线接点 | 简要信息常驻；全文按需打开 | 新知识、任务目标、前置条件和解锁成果 |
| 覆盖层 | 手册、设置、成功确认 | 明确进入这些状态 | 暂时接管操作，关闭后返回同一现场 |

1600×900 下，0.2 在展开测量区时的装配视口是 1066×493；0.3 是 1528×617。临时器材会遮住其中一部分，但可以收起或移动；没有固定的空白属性栏。减少常驻面板没有改变关卡依赖、知识点、验收规则或数学引擎。

## 美术约束与代码边界

风格为复古工业维修工作台：低饱和木色、深绿工作垫、浅色搪瓷部件、黄铜接头、磷光绿读数。材质层次来自轮廓、边缘、凹槽和投影，不使用网页式卡片阴影作为主要语言。旋钮、插孔和仪表拥有与用途对应的形状；正文用思源黑体，数字用 IBM Plex Mono。

本轮没有复制以上仓库的实现或精灵到游戏。研究快照仅用于阅读，产品中的几何、皮肤和纹理为独立原创。Mindustry、shapez、OpenRA、OpenRCT2 的仓库使用 GPLv3；OpenTTD 的 `COPYING.md` 是 GPLv2 文本，具体以仓库及对应文件为准。已使用的字体仍保留完整 OFL 授权。

研究得到的是适合这款游戏的设计判断，不是“像素边框就能让所有 UI 成为游戏”的定律。当前 0.3 仍是可玩的界面与操作原型，未达到 Steam 正式版的美术完成度。还需要统一插画/物件美术、完整音效、输入适配、首次操作引导以及后续张量关卡；现有截图与可执行版展示的是已经实现的部分。

## 验证与查看

打开 `docs/ui-art-direction-preview.html` 查看实际 Unity 0.3 截图和 0.2 对照。旧版查看器保存在 `docs/ui-art-direction-preview-0.2.html`。运行 `UnityGame/Builds/Windows-UI-0.3/LearningFoundry.exe` 才能操作游戏；浏览器只负责查看记录。

`verification/ui-0.3/` 保存真实玩家运行截图、训练检查和新增交互检查。训练图截图使用内存中的诊断模型，布置位置经过整理，执行真实 CPU 训练，不读写玩家存档。零件预览截图展示真实 Unity 放置状态；测试通过与正式输入相同的事件处理器模拟指针。
