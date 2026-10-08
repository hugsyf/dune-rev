实施状态：A／B／C 与额外两轮迭代已落实，实机反馈修订后纳入 2.3.0 发布；
重复的 Custom Fields 页签已撤回。以下保留原始评估，具体取舍、开关和交付见
[实施记录](IMPLEMENTATION.zh-CN.md)。

# Dune Rev 后续功能与改进评估

调研日期：2026-10-08。基线提交：81818ed，主题版本：2.2.0。
本文是候选计划，不代表这些功能已经实现，也不改变正式发布安排。

## 建议方向

先整理双视图共用布局、补充显示设置，再加入有明确用途的插件入口。
优先新增可编辑评分、Play Notes 和 Game Relations；媒体与自定义字段随后推进。
保持 Dune 的清晰卡片布局，避免每增加一个插件就增加一张常驻摘要卡。

## 本地项目检查结果

| 已有实现 | 对计划的影响 |
| --- | --- |
| 成就、通关估时、活动、配置、语言与 DLC 摘要及展开区 | 已经具备主要插件信息，不重复规划相同摘要 |
| 评论、新闻、Logo、视频、背景、ScreenshotsVisualizer | 后续媒体工作应整合已有入口 |
| 平台／来源横幅及两个默认开启的开关 | 接下来完善优先级、多平台与缩放，而非重新添加标签功能 |
| 社区／媒体／个人评分、安装目录与安装大小 | 有展示能力；个人评分目前不是内联编辑，安装大小不必新增重复卡片 |
| 原生笔记使用只读 TextBox | Play Notes 可补充编辑、Markdown 与多笔记功能 |
| SummaryCards.xml、OverviewDetails.xml 共享源文件 | 可沿用此机制扩展新内容，保留现有控件名称与作用域 |
| Hero 与顶部游戏信息分别维护在两个 overview 模板中 | 是优先整理点；最近的对齐修复必须在两处同步 |
| Grid Details 宽度由固定设置控制，媒体断点为 720，元数据断点为 900 | 可以增加紧凑布局与宽度预设；真正的拖动尺寸记忆需要另行核对宿主支持 |
| 两个 Hero 都有 Radius=120 的模糊效果 | 可提供减少模糊／动画的选项；尚未测量性能，不能据此断言它是瓶颈 |
| LibraryListView 主要承接原生 GamesGridView；搜索已有游戏与附加信息模板 | 列表、搜索的视觉一致性适合后续打磨，无须替换原生搜索机制 |

本地依据：Source/Views/DetailsViewGameOverview.xaml、GridViewGameOverview.xaml、LibraryGridView.xaml、LibraryListView.xaml、SearchView.xaml；Source/DerivedStyles/GridViewItemTemplate.xaml、GridViewItemStyle.xaml；Source/Constants.xaml、thememodifier.yaml、Common.xaml；tools/README.md 及共享片段。

## 在线对照与可借鉴之处

| 主题／扩展 | 已确认的能力 | 适合 Dune 的借鉴 |
| --- | --- | --- |
| [Helium](https://github.com/darklinkpower/Helium/blob/master/source/thememodifier.yaml) | 详情布局与网格内容顺序设置 | 先增加少量显示／布局预设；自由排序放到后期 |
| [Neon](https://github.com/XenorPLxx/Neon) | 可选的 Game Relations、Play Notes、PlayState、Steam Screenshots 等集成 | 参考插件入口位置与无插件时的回退，保留 Dune 的信息层级 |
| [eMixedNite](https://github.com/eminaguil/eMixedNite) | 临时 Quick Settings、标题与滚动布局设置 | 增加常用主题设置入口；临时预览与持久保存必须区分 |
| [KNARZnite](https://github.com/HerrKnarz/Playnite-Theme-KNARZnite) | 可编辑评分、自定义字段、多个插件集成 | 提升详情页的操作能力，同时保留原生字段作为回退 |
| [Harmony](https://github.com/darklinkpower/Harmony)、[Mythic](https://github.com/darklinkpower/Mythic) | 强调清晰层级、统一配色与可选插件 | 保持默认页面简洁，新模块有数据才出现 |

已阅读 Neon 的实际 overview 代码以及 Game Relations、Play Notes 的插件注册代码，确认后两者确实公开主题控件，不只是 README 中提及。以上比较不代表已在本地运行这些主题，亦不据仓库热度判断兼容性。

## 分批实施建议

规模为相对判断：小＝局部样式／控件入口；中＝双视图、设置与回退协同；大＝多个模块或新的状态管理。

| 顺序 | 候选项 | 用户收益与范围 | 依赖／规模 |
| --- | --- | --- | --- |
| A1 | 共用 Hero 信息行与操作样式 | 发售日、平台、来源、人数、标签及媒体控件的间距与对齐统一维护；先抽取样式／共享片段，保持外观 | 原生主题；中 |
| A2 | 紧凑布局与媒体偏好 | 提供紧凑／标准布局、Hero 高度或比例、背景暗化、减少模糊／动效选项；继续沿用已有 Logo 尺寸设置 | ThemeModifier；中 |
| A3 | 常用设置入口 | 让用户更容易找到标签总开关、来源偏好、卡片显示及主题设置；初版打开持久设置，临时预览后续再做 | ThemeModifier；小至中，打开设置命令待核对 |
| A4 | 横幅选择与缩放完善 | 明确平台／来源／插件 ID／默认横幅的优先级；核对 Windows、Linux、Mac 和多平台游戏；确保平台模式、来源模式尊重约定的自定义资源 | ThemeExtras 与原生回退；中 |
| B1 | 详情页直接编辑个人评分 | 在现有个人评分区域增加编辑，不另外建评分卡；有插件用原生评分控件，无插件保留现有展示与编辑游戏入口 | ThemeExtras；小 |
| B2 | Play Notes 编辑入口 | 在“笔记”区域提供原生只读笔记与 Play Notes 编辑模式，不混写两种数据；适合进度记录、攻略与待办 | Play Notes；中 |
| B3 | Game Relations 关联游戏 | 增加独立“关联”页签，优先同系列与相似游戏，再考虑同开发商／发行商；只显示库内结果，避免暗示全网推荐 | Game Relations；中 |
| C1 | 截图入口整合 | 区分个人截图与商店截图，统一卡片、滚动和空状态；先选一个商店截图方案，保留现有 ScreenshotsVisualizer | Steam Store Screenshots Viewer 或 Screenshot Utilities；中 |
| C2 | 可选自定义元数据字段 | 将冗长标签拆为“视角”“玩法”“兼容性”等字段，并允许插件提供编辑；默认保留原生标签与类型 | Metadata Utilities；中至大 |
| C3 | 网格浏览信息优化 | 可选个人评分／成就进度小标记、完整标题提示、悬停副本选择；同一角落只显示有限信息 | 原生、Playnite Achievements、DuplicateHider；中 |

### A 批：先提高一致性与可配置性

先做 A1，再做 A2/A3，最后完善 A4。近期修复已确认有效，因此 A1 应以保持现有视觉为目标，不在整理代码时重新设计页面。

A4 的本地观察是：文字回退的 PC 来源判断目前明确检查 pc_windows；Linux、Mac 和多平台组合需要纳入规则说明与真实环境核对。平台模式使用原生资源查找，来源模式使用 ThemeExtras，二者对用户覆盖图片的处理也需要明确。这里是规则与兼容性改进候选，并非已确认的新缺陷。

A2 的默认外观保持当前风格。建议“减少动效”默认关闭，“紧凑布局”默认关闭；详细设置用明确名称，避免数十个位置开关相互冲突。

### B 批：新增最有实际用途的操作与浏览

个人评分控件由 ThemeExtras 公开，支持主题中的可编辑评分；实现时沿用插件自己的评分换算，不在主题中自行维护另一套评分值。[控件文档](https://github.com/felixkmh/ThemeExtras-for-Playnite/wiki/Custom-UI-Elements)

Play Notes 支持 Markdown 和多笔记，并公开 NotesViewerControl。建议以安装状态与插件可见性为条件，在原生笔记旁提供清晰入口；新增集成可默认启用，但不强制用户安装插件。[插件介绍](https://github.com/darklinkpower/PlayniteExtensionsCollection)、[注册代码](https://github.com/darklinkpower/PlayniteExtensionsCollection/blob/master/source/Generic/PlayNotes/PlayNotes.cs)

Game Relations 公开 SimilarGamesControl、SameSeriesControl、SameDeveloperControl、SamePublisherControl。第一版仅接入两个最常用分类，结果较多时限制首屏长度。不要让“暂无关联”抢占整个详情页面。[注册代码](https://github.com/darklinkpower/PlayniteExtensionsCollection/blob/master/source/Generic/GameRelations/GameRelations.cs)

### C 批：扩大内容能力，但控制依赖

Steam Store Screenshots Viewer 提供商店截图；Screenshot Utilities 支持不同截图来源且需要相应 Provider。第一版应先选定一个方案，避免同时加载多个图库控件导致重复内容；具体控件与发布版本兼容性仍需核对。[Steam 截图插件集合](https://github.com/darklinkpower/PlayniteExtensionsCollection)、[Screenshot Utilities 介绍](https://github.com/HerrKnarz/Playnite-Extensions#screenshot-utilities)

Metadata Utilities 的官方文档确认它支持前缀虚拟字段、隐藏值及直接添加／删除值。其控件有自己的布局，需要额外适配 Dune 的卡片；建议默认关闭增强模式，启用且可用时才替换原生区域。[前缀与主题功能](https://knarzwerk.de/en/playnite-extensions/metadata-utilities/prefixes/)、[主题控件与样式](https://knarzwerk.de/?p=2387)

## 作为兼容性项目或暂缓

- 首页与书架：优先适配 StartPage 的配色、按钮和尺寸。它已经提供可配置书架及活动视图，不宜由主题重新实现一个数据首页；它文档中的部分成就功能仍提及 SuccessStory，与本主题的 Playnite Achievements 组合需要单独确认。[StartPage](https://github.com/felixkmh/StartPage-for-Playnite)
- 自动网格列数与悬停详情：先评估 Autogrid 和 GameHoverDetails 的视觉兼容，已有插件负责这些行为。不要同时叠加主题的大型悬停预览与插件弹窗。[插件说明](https://github.com/danitesler/playnite-extensions)
- 搜索：先改善原生搜索结果的间距、长标题与颜色。模糊搜索、过滤命令等可以使用 QuickSearch，主题主要保持其视觉协调。[QuickSearch](https://github.com/felixkmh/QuickSearch-for-Playnite)
- 暂停／恢复游戏：PlayState 可以提供该操作；如果用户实际使用，再接入其条件显示按钮。它控制游戏进程的行为属于插件，不属于 XAML 布局实现。[PlayState 介绍](https://github.com/darklinkpower/PlayniteExtensionsCollection)
- Steam 下载状态：Neon 源码已有 SteamGameStatusDetector 的状态和进度绑定，但本轮未核实其独立插件发布与当前支持范围，先保留为待调查项，不承诺能稳定展示所有安装／更新状态。[Neon 实现](https://github.com/XenorPLxx/Neon/blob/master/source/Views/DetailsViewGameOverview.xaml)
- 暂缓任意拖拽排序与位置持久化、大量动态壁纸、自动播放新行为、主题自带网页浏览器。这些项的状态管理、维护或资源开销较大，应在前述改进稳定后再决定。

## 实施与验收边界

1. 保持主题与安装清单版本 2.2.0；正式版本更新、打标签和发布另行安排。
2. 每批只实现一个清晰目标；插件集成均可选，卸载插件后保留原生内容或自动隐藏。
3. 使用插件公开的控件、属性与命令；实际开发时以安装／发布版本为准。上游集合明确提示 Extra Metadata Loader 的主分支有重写内容，不能把它直接当作当前稳定版接口。[上游说明](https://github.com/darklinkpower/PlayniteExtensionsCollection#contribution-guidelines)
4. 保留本地原生与插件控件名称；共享内容修改其源片段，避免两个视图产生差异。
5. 不编写模拟 Playnite／插件环境的测试脚本。完成代码审阅后打包，由真实 Playnite 环境确认窗口大小、缩放、长标题、缺失插件／数据以及切换游戏的显示。
6. 每批说明改动、默认开关、依赖和已知限制，不将未实测的推断写成已确认兼容性结论。

建议下一次明确选择 A 批，或先做 B1 的小范围功能。它们都能保持现有页面的结构，并为之后的插件扩展减少维护负担。


## 2026-10-08 实机反馈更新

C2 的独立 Custom Fields 页签已撤回：它重复原生标签、分类和类型，固定两列布局也不适合宽页面。后续若需要前缀虚拟字段，应在原生字段的位置替代显示，避免再建立重复页面。
