# Dune Rev

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![Playnite](https://img.shields.io/badge/Playnite-Desktop-6c5ce7.svg)](https://playnite.link/)
![DownloadCount](https://img.shields.io/github/downloads/hugsyf/dune-rev/total.svg)

[English](#english) · [简体中文](#简体中文)

## English

Dune Rev is a personal fork of [Dune](https://github.com/sakasakiking/Dune),
maintained by [hugsyf](https://github.com/hugsyf). It preserves Dune's clean,
spacious, Fluent-inspired design while improving extension compatibility,
layout flexibility, readability, and interaction feedback.

### Highlights

- Responsive Details and Grid views with game logos, screenshots, and videos.
- Adaptive summary cards for achievements, activity, play time, HLTB, and
  system requirements.
- Fluent hover feedback for cards that open extension views.
- Extension-aware backgrounds, duplicate selectors, favorites, completion
  status, feature icons, reviews, news, and player counts.
- Consistent colors for extension settings and embedded controls.
- ThemeModifier options for layout sizing and presentation preferences.

### Screenshots

![Dune Rev Details View](Screenshots/Details%20View.png)
![Dune Rev Grid Details View](Screenshots/Grid%20Details%20View.png)

### Installation

1. Download the latest `.pthm` file from the
   [Releases page](https://github.com/hugsyf/dune-rev/releases).
2. Open the downloaded file and let Playnite install the theme.
3. In Playnite, select **Dune Rev** under Desktop theme settings.

You can also install it via this [link](playnite://playnite/installaddon/DuneRev_aa8df0f9-9406-4ea6-a31a-3bd13853fd40), it is also available from Playnite's built-in add-on browser.

### Recommended setup

- Install the **Segoe Fluent Icons** font if it is unavailable on the system.
  Microsoft provides it with the
  [Windows design resources](https://learn.microsoft.com/windows/apps/design/downloads/#fonts).
- Details View uses a 1760 px maximum content width and a 480 px information
  column by default, leaving the summary-card area enough room on common
  widescreen displays.
- Grid View uses a 640 px details pane by default. Its summary cards wrap into
  additional rows, and the information panel is placed below them so narrow
  sidebars do not compress either section.
- Content, pane, card, logo, extension-panel, action-button, and top-bar sizes
  can be adjusted through ThemeModifier. Options are grouped by view and use
  consistent bilingual English and Simplified Chinese labels.
- Restart Playnite after installing or updating extensions that inject theme
  controls.

### Extension compatibility

| Feature | Extension |
| --- | --- |
| Achievements | SuccessStory and Playnite Achievements automatic migration |
| Alternative backgrounds | BackgroundChanger |
| Duplicate copies | DuplicateHider |
| Feature icons | Library Management |
| Favorites and completion controls | ThemeExtras |
| Activity and statistics | GameActivity |
| Completion estimates | HowLongToBeat |
| Logos and videos | Extra Metadata Loader |
| Screenshots | ScreenshotsVisualizer |
| Steam news and players online | Steam News and Players Viewer |
| Steam reviews | Review Viewer |
| System requirements | SystemChecker |
| Theme customization | ThemeModifier |

Extensions are optional. Their cards and tabs are hidden when an extension is
unavailable or has no data for the selected game.

### Credits and license

Dune Rev is based on **Dune** by
[sakasakiking](https://github.com/sakasakiking). The extension integration
approach also takes inspiration from Playnite themes such as Mythic.

Distributed under the [MIT License](LICENSE). The original copyright notice is
retained as required by the license.

### Support

Please report Dune Rev problems through the
[issue tracker](https://github.com/hugsyf/dune-rev/issues). For an issue that is
also reproducible in unmodified Dune, consult the
[upstream project](https://github.com/sakasakiking/Dune).

---

## 简体中文

Dune Rev 是 [Dune](https://github.com/sakasakiking/Dune) 的个人分支，由
[hugsyf](https://github.com/hugsyf) 维护。它保留了 Dune 简洁、宽松的
Fluent 风格设计，同时改善了扩展兼容性、布局灵活性、内容可读性与交互反馈。

### 主要特性

- 响应式详情视图与网格视图，支持游戏 Logo、截图和视频。
- 可自适应排列的概览卡片，可显示成就、活动记录、游玩时间、HLTB 和系统需求。
- 可打开扩展页面的卡片带有 Fluent 风格的悬停反馈。
- 支持替代背景、重复副本选择、收藏与完成状态、功能图标、评论、新闻和在线人数。
- 改善扩展设置页面与嵌入式控件的配色一致性。
- 通过 ThemeModifier 调整布局尺寸与显示偏好。

### 截图

![Dune Rev 详情视图](Screenshots/Details%20View.png)
![Dune Rev 网格详情视图](Screenshots/Grid%20Details%20View.png)

### 安装

1. 从 [Releases 页面](https://github.com/hugsyf/dune-rev/releases)下载最新版
   `.pthm` 文件。
2. 打开下载的文件，让 Playnite 安装主题。
3. 在 Playnite 的桌面主题设置中选择 **Dune Rev**。

也可直接点击该[链接](playnite://playnite/installaddon/DuneRev_aa8df0f9-9406-4ea6-a31a-3bd13853fd40)，或通过 Playnite 内置的附加组件浏览器安装。

### 推荐设置

- 如果系统中没有 **Segoe Fluent Icons** 字体，请先安装。Microsoft 在
  [Windows 设计资源页面](https://learn.microsoft.com/windows/apps/design/downloads/#fonts)
  提供该字体。
- 详情视图默认将总览内容最大宽度设为 1760 px、游戏信息栏最大宽度设为
  480 px，使常见宽屏分辨率下的概览卡片保有足够空间。
- 网格视图默认使用 640 px 宽的详情栏。概览卡片会自动换行，游戏信息面板位于
  卡片下方，避免狭窄侧栏过度挤压内容。
- 内容区域、详情栏、卡片、Logo、扩展面板、操作按钮和顶部栏等尺寸均可通过
  ThemeModifier 调整。选项按视图分类，并使用统一的英中双语名称。
- 安装或更新会向主题注入控件的扩展后，建议重启 Playnite。

### 扩展兼容性

| 功能 | 扩展 |
| --- | --- |
| 成就 | SuccessStory，以及 Playnite Achievements 的自动主题迁移 |
| 替代背景 | BackgroundChanger |
| 重复副本 | DuplicateHider |
| 功能图标 | Library Management |
| 收藏与完成状态控制 | ThemeExtras |
| 活动记录与统计 | GameActivity |
| 通关时间估算 | HowLongToBeat |
| Logo 与视频 | Extra Metadata Loader |
| 截图 | ScreenshotsVisualizer |
| Steam 新闻与在线人数 | Steam News and Players Viewer |
| Steam 评论 | Review Viewer |
| 系统需求 | SystemChecker |
| 主题自定义 | ThemeModifier |

所有扩展均为可选依赖。扩展未安装，或当前游戏没有相应数据时，对应卡片和标签页
会自动隐藏。

### 致谢与许可

Dune Rev 基于 [sakasakiking](https://github.com/sakasakiking) 制作的
**Dune**。扩展集成方式也参考了 Mythic 等 Playnite 主题。

本项目采用 [MIT License](LICENSE) 发布，并依照许可证要求保留原始版权声明。

### 问题反馈

Dune Rev 的问题请提交至 [Issue Tracker](https://github.com/hugsyf/dune-rev/issues)。
如果问题在未经修改的 Dune 中也能复现，请同时参考
[上游项目](https://github.com/sakasakiking/Dune)。
