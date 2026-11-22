# 🌟 Pathos: Nethack Codex 随机种子分析与筛选器 (Seed Finder & Analyzer)

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-lightgrey.svg)]()

<p align="center">
  <b>简体中文</b> | <a href="README_en.md">English</a>
</p>

专为 Roguelike 经典神作《**Pathos: Nethack Codex**》打造的高性能随机种子（Seed）透视、筛选与地牢地图可视化工具。

基于游戏的底层引擎模拟算法开发，直接在内存中执行种子地牢生成，兼具毫秒级推演速度、100% 精确的真机还原度，以及直观华丽的现代 Web 可视化界面！

---

## ✨ 核心特性

### 1. 🖥️ 现代化 WebGUI 可视化控制台
- **自动唤起交互界面**：双击启动后自动在本地浏览器中呈现完整的现代化操作控制台。
- **地牢全图与瓦片渲染 (Map & Tile Visualizer)**：
  - 支持主地牢、矿洞、精灵王国、仓库番、美杜莎、深渊等所有分支区域的层级切换与地图查看。
  - 动态解析游戏官方 `.Atlas` 贴图包，完美还原官方瓦片（支持 `Sonyvanda`、`Classic`、`Absurd`、`Geoduck` 等各套图集）。
- **多线程并发控制面板**：可视化调节并发 Worker 线程池规模，全速压榨 CPU 算力进行批量 Roll 种。
- **全界面双语无缝切换**：深度适配中英文双语环境。

### 2. 🔍 种子全景情报深度透视 (Analyze)
- 🏰 **矿镇生态判定**：自动识别矿镇（Minetown）状态，精确判定是**秩序井然/守卫巡逻（Peaceful）**还是惨遭**兽人攻城屠戮（Orcish Massacre）**，并统计警卫、兽人与商人数量。
- 🧝 **精灵王国政局**：智能研判精灵古国繁荣度（和平友好 / 兽人进犯），定位王座与国宝神器所在。
- 🗡️ **全神器精准定位**：统计当局生成的所有神器（中英文双语显示），精确标明所在分支、具体层数、坐标，以及存放形式（地面散落、密室宝箱、怪物携带）。
- 🏪 **商店与 NPC 坐标索引**：精准列出百货商店、古兵器店、二手盔甲店、祭坛、黑市等商户类型、店主姓名与坐标。
- 🧪 **全知破译表 (Omniscience ID Table)**：直接破解药水、卷轴、魔杖的外观对应表（如“玫红药水 ➜ 暴怒药水”），告别神农尝百草！
- 📦 **仓库番 (Sokoban) 终极大奖侦测**：提前透视第 4 层三大件（收纳之袋 / 续命护符 / 防护斗篷）分布。

### 3. ⚡ 超高速多维度条件筛选 (Filter / Roll)
- 🎯 **指定神器搜索**：支持中文或英文名模糊/精确匹配（例如想要 `明镜` 或 `Mirrorbright`）。
- 🪜 **深度与区域限定**：指定神器必须出现在特定深度或特定区域（例如要求在第 1 层或第 3 层拿到）。
- 📦 **神器数量门槛**：指定当局游戏至少生成 $N$ 件神器（如 $\ge 20$ 件高爆种子）。
- ⚔️ **生态指定筛选**：指定只要“守卫驻防”或只要“兽人屠城”的特定生态。
- 🚀 **极速推演**：多核并行下每秒可推演数百个完整种子，数秒内即可筛出心仪的天胡开局！

---

## 🚀 快速启动

### 方式一：双击一键运行（强烈推荐）

在项目根目录下双击运行 **`run.bat`**：
1. 程序会自动启动本地轻量 Web 服务（默认监听 `http://127.0.0.1:5218`）。
2. **自动在你的默认浏览器中打开现代可视化控制台**！
3. 首次使用时，只需在 Web 页面顶部填入你的本地《Pathos》游戏安装路径（如 `G:\games\Pathos` 或 Steam 安装目录），点击保存后即可立即开始分析与筛选。

---

### 方式二：终端与命令行模式 (CLI)

如果你习惯纯黑底绿字的终端命令行，工具同样提供完备的控制台交互与自动化参数支持：

#### 1. 交互式终端控制台
```bash
cd PathosSeedFinder
dotnet run -- cli
```

#### 2. 命令行直接分析指定种子
```bash
dotnet run -- analyze -s <种子名称或代号>
```
*示例：*
```bash
dotnet run -- analyze -s test1234
dotnet run -- analyze -s seed_77217
```

#### 3. 命令行条件检索与筛选
```bash
dotnet run -- filter [选项]
```

| 参数 | 缩写 | 说明 | 示例 |
| :--- | :--- | :--- | :--- |
| `--artifact <名称>` | `-art` | 必须包含的神器（支持中文或英文） | `--artifact 明镜` |
| `--level <层数>` | `-l` | 指定神器必须出现的深度 | `--level 1` 或 `--level 3` |
| `--site <区域>` | 无 | 指定神器所在的区域名称 | `--site "Elf Kingdom"` |
| `--min-artifacts <数量>` | `-m` | 要求的最少神器总数量 | `--min-artifacts 18` |
| `--minetown <状态>` | `-mt` | 矿镇状态 (`normal` 正常 / `orc` 兽人攻城) | `--minetown orc` |
| `--max <检索上限>` | `-n` | 最多测试的种子数量 | `--max 500` |

*示例：*
```bash
# 寻找开局包含“明镜”的神器种子
dotnet run -- filter --artifact 明镜 --max 100

# 寻找兽人攻城状态的矿镇种子
dotnet run -- filter --minetown orc --max 100

# 寻找至少 18 件神器的高爆天胡种子
dotnet run -- filter --min-artifacts 18 --max 200
```

---

## 🛠️ 开发者构建与技术架构

- **技术栈**：.NET 8.0 SDK / C# 12 / HTML5 Canvas
- **推演机制**：直接挂载游戏核心引擎库 `PathosEngine.dll`、`Inv.Library.dll` 及 `PathosOfficial.dll`，在独立 `AssemblyLoadContext` 中并行推演，完全杜绝模拟偏差，100% 还原真机生成逻辑。
- **自定义编译**：若本地游戏安装在非默认路径，可通过 MSBuild 属性自定义编译：
  ```bash
  dotnet build -p:PathosDir="D:\Your\Pathos\GamePath"
  ```
  *(注：工程程序集名称保留为 `PathosSyntaxTest`，以满足游戏底层内部类的可见性权限校验)*

---

## 🤝 致谢与鸣谢 (Acknowledgments)

- **游戏原著**：致敬经典的 Roguelike 神作《Pathos: Nethack Codex》及其作者 **Callan O'Neill**，感谢其带来的奇妙地牢探索体验。
- **技术协同**：本项目由人类开发者完成系统架构构思、领域推演建模与全流程质量验收，并深度结合 **Google Gemini (Google DeepMind)** 的前沿代码与工程化能力协同辅助构建。

---

## 📜 开源协议与版权声明

- 本项目采用 [MIT License](LICENSE) 开源协议。
- **版权声明**：《Pathos: Nethack Codex》版权归原作者 **Callan O'Neill** 所有。本项目为第三方开源辅助研究工具，不分发任何受版权保护的官方游戏二进制文件或专有素材。用户需自行拥有合法正版游戏本体。
