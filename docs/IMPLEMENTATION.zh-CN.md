# 分批实施记录

开发分支：codex/theme-improvements。版本继续保持 2.2.0。
各批代码审阅、打包后只提交本地，不推送、不打标签、不正式发布。
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
- 默认关闭：紧凑布局、减少封面动效、个人评分封面标记、自定义字段页、优先使用截图网格。
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
