# 分批实施记录

开发分支：codex/theme-improvements。版本继续保持 2.2.0。
各批代码审阅、打包后只提交本地，不推送、不打标签、不正式发布。
不编写或运行模拟 Playnite／插件环境的测试脚本。

| 批次 | 目标 | 状态 |
| --- | --- | --- |
| A | 共用信息行、紧凑／媒体设置、设置入口、横幅规则 | 已审阅、打包并本地提交 3b8aeaf |
| B | 可编辑评分、Play Notes、Game Relations | 已审阅、打包并本地提交 f7324b2 |
| C | 商店截图、自定义字段、网格浏览信息 | 已审阅、已打包；本批本地提交 |
| 迭代 1 | 重新检查本地与在线调研，选择并实施改进 | 待调研 |
| 迭代 2 | 再次检查本地与在线调研，选择并实施改进 | 待调研 |

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