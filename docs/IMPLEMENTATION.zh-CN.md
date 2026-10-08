# 分批实施记录

开发分支：codex/theme-improvements。开发阶段的版本保持 2.2.0。
各批代码审阅、打包后只提交本地；2026-10-08 用户授权统一发布为 2.3.0。
以下保留各开发批次的原始记录，当时的版本号与发布限制属于历史状态。
不编写或运行模拟 Playnite／插件环境的测试脚本。

| 批次 | 目标 | 状态 |
| --- | --- | --- |
| A | 共用信息行、紧凑／媒体设置、设置入口、横幅规则 | 已审阅、打包并本地提交 3b8aeaf |
| B | 可编辑评分、Play Notes、Game Relations | 已审阅、打包并本地提交 f7324b2 |
| C | 商店截图、自定义字段、网格浏览信息 | 已审阅、打包并本地提交 f7cb07d |
| 迭代 1 | 标题回退、减少动效完善、键盘与媒体可访问性 | 已审阅、打包并本地提交 2abccc6 |
| 迭代 2 | 原生截图网格、空页签与字段回退、评分入口完善 | 已审阅、打包并本地提交 |

## A 批

- HeroMetadata.xml 共享顶部信息行，保留两视图原有布局触发器和控件名。
- 紧凑 Hero、模糊强度、底部暗化、网格 Hero 参考高度和减少封面动效均可设置。
- 顶栏持久主题设置入口通过 ThemeExtras 的公开 OpenPluginSettingsCommand 打开 ThemeModifier。
- 横幅按第一个平台作为主平台；Windows／Linux／Mac 与无平台游戏可以选择来源。
  来源模式依次尝试主题目录的来源名称图片、插件 ID 图片、ThemeExtras、平台图片。
  平台模式直接使用平台资源；原生资源采用 Playnite 的缓存图片转换器。
- 不模拟插件运行环境；本批代码审阅后由 Toolbox 打包。

参考：[ThemeExtras 公开命令](https://github.com/felixkmh/ThemeExtras-for-Playnite/blob/master/source/ExtrasSettings.cs)、
[Helium 布局设置](https://github.com/darklinkpower/Helium/blob/master/source/thememodifier.yaml)。

## B 批

- 两种详情视图的个人评分区域使用 ThemeExtras 交互星级，关闭开关或缺少控件时回退原生评分。遵循 Playnite 评分字段可见性。
- Play Notes 独立折叠区域保留 Markdown／编辑能力，不混写原生笔记。
- 关联页签显示库内同系列与相似游戏，沿用插件的 IsVisible 状态；每个列表高度可调。
- 三个功能开关默认开启，插件缺失时自动隐藏。未在运行中的 Playnite 验证。

参考：[ThemeExtras 控件](https://github.com/felixkmh/ThemeExtras-for-Playnite/wiki/Custom-UI-Elements)、
[Play Notes](https://github.com/darklinkpower/PlayniteExtensionsCollection/wiki/Play-Notes)、
[Game Relations](https://github.com/darklinkpower/PlayniteExtensionsCollection/wiki/Game-Relations)。
## C 批

- 商店截图沿用 Steam Store Screenshots Viewer 的 SteamScreenshots_SteamScreenshotsViewControl 公开控件；与 ScreenshotsVisualizer 个人截图分开命名。
- Metadata Utilities 自定义字段独立页签（默认关闭），接入标签／分类／类型的前缀分组与插件原生编辑，遵循 Playnite 对应字段开关。
  原生字段仍保留：本轮选择独立页签，避免替换两视图不同的原生字段布局，用户可随时关闭增强内容。
- 封面与标题提供完整标题提示。可选个人评分只绑定当前封面的 Game.UserScore，默认关闭；不使用所选游戏的插件全局数据。
- DuplicateHider_SourceSelector1 在悬停或键盘聚焦时显示，默认开启，位于平台横幅下方；保留插件的控件回收逻辑。
- 商店截图默认开启；不重复叠加另一套截图插件。成就封面标记暂不加入，避免共享所选游戏数据造成错误标记。

参考：[Steam Screenshots 控件与设置](https://github.com/darklinkpower/PlayniteExtensionsCollection/wiki/Steam-Screenshots)、
[Metadata Utilities 主题集成](https://knarzwerk.de/en/playnite-extensions/metadata-utilities/theme-integration/)、
[DuplicateHider 网格与控件缓存](https://github.com/felixkmh/DuplicateHider#theme-integration)。
## 自主迭代 1

重新检查本地 Hero 与网格样式，在线对比 Helium 的 Logo／标题切换和 Extra Metadata Loader 的公开媒体接口。
选择缺媒体时的识别能力与操作可访问性，暂不增加另一套视频引擎。

- Logo 不可用或关闭时显示原生 PART_TextDisplayName，长标题可换行，图标统一限制在 64px。
  标题回退默认开启并可关闭，原生名称控件继续负责显示名称。
- 开启减少封面动效后，选中边框使用静态样式；循环光泽只在实际可见时运行，隐藏后停止。
- 网格游玩／详情操作也可通过键盘聚焦显示；副本选择沿用上一批的键盘入口。
- Hero 背景、视频播放／暂停、静音控件增加中英提示与辅助功能名称。
- 已代码审阅和 Toolbox 打包；未运行模拟界面测试，也未宣称真实环境性能提升。

参考：[Helium overview](https://github.com/darklinkpower/Helium/blob/master/source/Views/DetailsViewGameOverview.xaml)、
[Extra Metadata Loader 接口](https://github.com/darklinkpower/PlayniteExtensionsCollection/wiki/Extra-Metadata-Loader-theme-controls)、
[WPF 可控动画](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/storyboards-overview)。
## 自主迭代 2

再次检查本地插件布局与样式加载顺序，在线查阅 Neon overview、ScreenshotsVisualizer 图库布局及
Game Relations 的控件设置。决定完善已有插件面板，不增加新的截图下载或库数据维护逻辑。

- 个人截图总开关默认开启；原生网格图库偏好默认关闭。
  开启主题偏好且插件启用 EnableIntegrationPicturesList 时使用 PluginScreenshots 原生图库；
  控件缺失／不可用时回退现有预览与列表。图库高度沿用截图设置，纵向列表限制到 240px。
- 评分入口遵循 Playnite UserScore 字段开关，允许直接给未评分游戏打分；无插件且无评分时收起空评分区域。
- Metadata Utilities 保留 Tag 的图标字符约定，字段可见性独立绑定；所有控件隐藏时收起自定义字段页签。
- Game Relations 同时遵循 IsVisible 与 IsEnabled。语言控制移除固定 600px 最小宽度。
- Metadata Utilities 按钮样式移至原生 Button 样式之后，避免 Common 中的前向静态资源引用；
  紧凑布局的布尔资源统一通过 DataTrigger 判断。
- 每轮仅代码审阅与 Toolbox 打包；真实 Playnite 插件状态、窗口缩放和键盘操作仍需实际环境确认。

参考：[Neon overview](https://github.com/XenorPLxx/Neon/blob/master/source/Views/DetailsViewGameOverview.xaml)、
[ScreenshotsVisualizer 原生图库](https://github.com/Lacro59/playnite-screenshotsvisualizer-plugin/wiki/Gallery-layouts)、
[Game Relations 设置](https://github.com/darklinkpower/PlayniteExtensionsCollection/blob/master/source/Generic/GameRelations/Models/GameRelationsControlSettings.cs)。

## 开关与交付

- 默认开启：评分编辑、Play Notes、关联游戏、个人／商店截图、标题回退、副本选择、主题设置入口。
- 默认关闭：紧凑布局、减少封面动效、个人评分封面标记、优先使用截图网格。
- 各批安装包位于 release/batch-a、batch-b、batch-c、iteration-1、iteration-2；
  iteration-2 是完整累计包，版本仍为 2.2.0。包文件不纳入 Git。
- 无推送、合并、标签或正式发布；后续功能扩展以真实环境反馈为依据。

## 载入失败修复（2026-10-08）

- 本机 playnite.log 在 18:20:47.971 报告 DetailsViewGameOverview.xaml 载入失败：
  `Settings` 对 `Setter.Value` 无效。异常中的 2462 行指向模板结束，实际触发点是原第 36 行的行高 Setter。
- 将 CalculatedGameDetailsIndentation 先绑定到隐藏 Border.Tag，再通过普通 Binding 设置行高；
  保留原生行高与紧凑布局开关。新增个人截图、关联游戏和设置入口的 Setter 也改为常量＋绑定触发器。
- 同步两个 overview 及共享源片段，代码审阅后重新用 Toolbox 打包，不编写模拟测试。
- 修复包：release/iteration-2-hotfix；原 iteration-2 路径也更新为修复后的累计包。
  版本仍为 2.2.0，本地提交，不推送。载入成功需真实 Playnite 环境确认。

## 实机反馈：副本入口、Fluent 控件与 Playnite 横幅（2026-10-08）

- 单副本游戏不再出现额外的商店图标。多副本入口移到封面下方，悬停或键盘聚焦时显示，
  保留现有总开关；只有多副本卡片预留 28px，避免悬停造成跳动。当前来源用图标亮度标识。
  封面的选中边框约束在封面行，不包含下面的入口。
- 利用 DuplicateHider_ContentControl1 的公开 MoreThanOneCopy 属性判断副本数量。
  状态控件保持可见、零尺寸、零透明度且不可交互；插件在 IsVisible=False 时停止订阅游戏变化，
  因此不能使用 Collapsed/Hidden。实际来源选择仍由插件原生控件处理。
- Playnite／手动添加游戏优先显示第一个平台，避免用 Playnite 标记替代平台信息。
  判断手动库 PluginId 为空 GUID，或来源名称为 Playnite；新增默认开启的独立开关用于对照。
  缺少平台及图片时沿用文字／隐藏回退，不制造空横幅。
- 输入框统一 4px 圆角、半透明表面、细边框与聚焦底线；弹出列表统一 8px 圆角、细边框和行间距。
  复选框改用 20px 方框与矢量勾选，保留三态、键盘和禁用状态。
- 主题设置齿轮与筛选清除按钮默认透明；悬停／聚焦时轻微反馈。清除图标为普通叉号。
  主 Hero 的操作按钮保留原有表面，筛选器原生 PART 名称与处理流程保留。
- 代码审阅后打包至 release/visual-polish，版本 2.2.0，仅本地提交；实机显示仍待确认。

参考：[DuplicateHider 主题接口](https://github.com/felixkmh/DuplicateHider#theme-integration)、
[状态控件可见性处理](https://github.com/felixkmh/DuplicateHider/blob/master/source/Controls/DHContentControl.xaml.cs)、
[微软控件状态说明](https://learn.microsoft.com/en-us/windows/uwp/design/controls-and-patterns/control-templates)。

## 实机反馈：扩展页签与图表布局（2026-10-08）

- 用户后续截图确认 Play Notes 和商店截图能加载，原问题是它们藏在总览内的展开项中，
  不能继续将“无入口”解释为插件下载或加载失败。日志也已有商店图片缓存成功记录。
- 将 Play Notes、商店截图、个人截图改为与总览、库内关联游戏、评论、新闻并列的页签，
  保留原有显示开关。页签仅负责选择，内容常驻 overview 模板命名域，避免切页重建插件控件。
  隐藏当前页签时回到总览。笔记与商店截图使用当前控件状态，不依赖跨视图共享的 IsControlVisible。
- 扩展内容统一透明容器；关联游戏取消固定高度，保留最大高度设置，并让标题随实际内容隐藏，
  消除空内容条和单个游戏下方的巨大空白。评论和新闻同步移除主题额外添加的实色背景。
- Play Notes 保留原生笔记编辑、保存、切换、删除与攻略导入功能；工具按钮使用透明常态表面，
  移除撑满整行的分隔线。无笔记时提示新建或导入，不显示空实色底板。
- 商店截图通过已核对的原生控件公开 Screenshots、SelectedScreenshot、CurrentImageBitmap
  及查看器／前后切换命令构建预览与缩略图。插件继续负责下载和游戏上下文，主题明确分配预览高度；
  保留点击打开原生查看器、前后按钮与方向键。缩略图提供选中和键盘焦点边框。
- 本机 ScreenshotsVisualizer 的单图高度为 150，主题原来又在上方堆叠一个高 500 的竖向列表。
  改为大图与横向缩略图，横向图库可用时不再重复显示竖向图库；关闭横向图库时保留左侧竖向回退。
  单图的最小／最大高度同步约束为主题截图高度，原生网格图库偏好仍保留。
- 本机 GameActivity 的 ChartLogHeight 与 ChartTimeHeight 均为 120。
  该插件原生控件读取外层 ContentControl.MinHeight，因此性能图表设置最小高度 360，
  新增可调范围 260～600；历史图表最小高度 220。没有修改插件自身配置。
- 同步 Details 与 Grid Details 两套视图，保持版本 2.2.0。仅代码审阅、生成共享片段和 Toolbox 打包，
  不编写或运行模拟界面测试。包在 release/plugin-layout-polish，仅本地提交，不推送或发布。

参考：[Play Notes 原生控件](https://github.com/darklinkpower/PlayniteExtensionsCollection/blob/master/source/Generic/PlayNotes/PlayniteControls/NotesViewerControl.xaml)、
[Steam 商店截图控件](https://github.com/darklinkpower/PlayniteExtensionsCollection/blob/master/source/Generic/SteamScreenshots/ScreenshotsControl/SteamScreenshotsControl.xaml.cs)、
[个人截图高度约束](https://github.com/Lacro59/playnite-screenshotsvisualizer-plugin/blob/master/source/Controls/PluginSinglePicture.xaml)、
[性能图表高度约束](https://github.com/Lacro59/playnite-gameactivity-plugin/blob/master/source/Controls/PluginChartLog.xaml)。
## 实机反馈：按钮与弹层一致性（2026-10-08）

- 设置齿轮与原生 TopPanelItem 共用 DuneTopPanelButtonTemplate 和基础样式：32px 命中区域、
  20px 图标容器、相同圆角、0.16 悬停叠加与 0.4 秒动画。使用同一 TopPanelIconFontStyle，
  移除独立工具栏样式及额外间距。原生切换状态映射到 Selector.IsSelected，保留底部指示线。
  设置按钮保留自己的命令；原生 TopPanelItem.OnApplyTemplate 会重绑 Command、Content 等属性，
  因此共享视觉模板，而不直接用该控件替代设置按钮。
- 评论、新闻原来的 FontSize=16 与其他页签的动态字号确实不同。所有总览页签改为显式引用
  同一个 FontSize 与 FontFamily 资源；选中加粗和指示线仍保留。
- Play Notes、ScreenshotsVisualizer、ReviewViewer、NewsViewer 的操作由插件负责，主要控件是
  普通 WPF Button；商店截图导航是主题上轮新增的按钮，调用插件公开命令。此前笔记与商店截图
  的局部模板也是外观不一致的来源，本次合并为 DunePluginActionButton / DunePluginIconButton。
- 只在这些扩展宿主中应用共享样式：透明常态、相同 4px 圆角、36px 图标按钮、20px 图标容器，
  相同悬停／按下反馈。Viewbox 约束插件写死的 24/40 字号；保留图标字体、命令、事件、内容模板
  与禁用状态，文本操作按钮保持自然宽度。笔记不再保留上一轮独立按钮模板。
- ReviewViewer 顶部 Review Type / Purchase Type / Language / Playtime / Display 五个筛选块
  是插件自己绘制的 Grid/Border，使用控件内的 StaticResource 专用样式，不能通过普通 Button
  样式统一。本次保留这些区域，未替换原生筛选交互或修改插件文件。
- 普通 ComboBox、FilterSelectionBox、ComboBoxList 弹层统一使用 DuneDropdownPopupBorder，
  背景为 #B320242B（约 70% 不透明度）。Popup 继续允许透明，只对背景画刷设置 alpha，
  不对整个弹层设置 Opacity；文字、复选框、滚动条不会被淡化。并非 Acrylic 模糊材质。
- 已代码审阅、同步共享片段并用 Toolbox 打包；不编写测试脚本。版本仍为 2.2.0，
  包在 release/control-consistency，仅本地提交，不推送或发布，实际效果待用户确认。

参考：[TopPanelItem 行为](https://github.com/JosefNemec/Playnite/blob/master/source/Playnite.DesktopApp/Controls/TopPanelItem.cs)、
[ReviewViewer 控件与局部筛选样式](https://github.com/darklinkpower/PlayniteExtensionsCollection/blob/master/source/Generic/ReviewViewer/Presentation/ReviewsControl.xaml)、
[NewsViewer 原生按钮](https://github.com/darklinkpower/PlayniteExtensionsCollection/blob/master/source/Generic/NewsViewer/Presentation/NewsViewerControl.xaml)。
## 实机反馈：Details Hero Logo 对齐（2026-10-08）

- 标题回退改动 2abccc6 将 Logo／标题父 Grid 从 Left 改为 Stretch，给长标题提供换行宽度，
  但 Details 的 ExtraMetadataLoader_LogoLoaderControlGrid 仍为 HorizontalAlignment=Center。
  因此 Logo 从原来的自然宽度容器内居中，变为相对整个 Hero 居中。
- 将 Details Logo 容器显式改为 Left，与 Grid Details 的对齐一致；父 Grid 继续 Stretch，
  保留标题回退与原来的 Logo 尺寸设置。此次未修改共享片段，无需重新生成扩展布局。
- 已审阅改动并打包至 release/details-logo-alignment；版本保持 2.2.0，仅本地提交，
  不推送或发布，不编写测试脚本。真实界面效果由用户确认。
## 实机反馈：撤回重复的 Custom Fields 页签（2026-10-08）

- 实机截图显示该页签仍展示原生 Tags、Categories、Genres，并与现有详情栏／Hero 字段重复。
  Metadata Utilities 的这些控件处理的是已有字段的前缀分组、值隐藏和编辑；此前命名为
  Custom Fields 容易让人误以为存在一组独立的新增属性。
- 官方控件布局是左右等宽列。左侧字段名占据半页，右侧逐行显示值，叠加主题的
  MdStyleItemButton.HorizontalAlignment=Stretch 与实色底板，形成大面积留白和长条按钮。
- 移除独立页签及三个插件宿主，保留原生字段的展示与筛选。同步移除显示开关、默认值、
  中英标题资源、专用 Metadata 样式和已无引用的 DunePluginPanel，清理 README 与待发布 changelog。
  不修改插件文件、配置或游戏数据。此前实施记录保留为历史，本段说明该功能已撤回。
- 如果今后确实需要前缀虚拟字段，应在详情栏内作为原生对应字段的替代，而不另建重复页签；
  本轮不增加该集成，避免继续扩大未经用户需要确认的功能范围。
- Details 与 Grid Details 同步生成；代码审阅后由 Toolbox 打包至 release/remove-custom-fields。
  版本仍为 2.2.0，仅本地提交，不推送或发布，不编写测试脚本。

参考：[Metadata Utilities 控件及固定布局说明](https://knarzwerk.de/en/playnite-extensions/metadata-utilities/theme-integration/)。
