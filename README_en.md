# 🌟 Pathos: Nethack Codex Seed Finder & Analyzer

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-lightgrey.svg)]()

<p align="center">
  <a href="README.md">简体中文</a> | <b>English</b>
</p>

A high-performance random seed analyzer, filter, and dungeon map visualizer built specifically for the classic Roguelike masterpiece ***Pathos: Nethack Codex***.

Directly driven by the game's core simulation engine, executing dungeon generation entirely in-memory with sub-second analysis speeds, 100% genuine parity, and an intuitive modern Web visual interface!

---

## ✨ Key Features

### 1. 🖥️ Modern WebGUI Visual Console
- **Auto-Launching Dashboard**: Double-clicking `run.bat` boots a local web server and automatically opens the visual dashboard in your default browser.
- **Full Dungeon Map & Tile Visualizer**:
  - Browse and switch between all branches and floors: Main Dungeon, Minetown & Mines, Elf Kingdom, Sokoban, Medusa Lair, Lich Tower, Abyss, and more.
  - Dynamic extraction of official `.Atlas` packages, faithfully rendering game tiles with 1:1 fidelity across official tilesets (`Sonyvanda`, `Classic`, `Absurd`, `Geoduck`).
- **Multi-Threaded Concurrency Control**: Graphically adjust the Worker thread pool to leverage all CPU cores for high-speed batch rolling.
- **Seamless Bilingual Support**: Complete language switching between English and Simplified Chinese.

### 2. 🔍 Deep Seed Intelligence Analysis (Analyze)
- 🏰 **Minetown Ecological State**: Automatically diagnoses whether Minetown is **Peaceful & Guarded** or suffered an **Orcish Massacre**, complete with guard, orc, and merchant censuses.
- 🧝 **Elf Kingdom Political Climate**: Diagnoses the state of the Elven court (Prosperous & Peaceful vs. Invaded), locating the throne and the royal artifact.
- 🗡️ **Precise Artifact Locator**: Catalogs all generated artifacts in the current run (bilingual names), detailing their exact branch, floor, coordinates, and storage form (loose on ground, locked in container/chest, carried by boss/monster).
- 🏪 **Store & NPC Directory**: Comprehensive index of general stores, antique weapon outlets, second-hand armorers, holy shrines, and the Black Market, along with shopkeeper names and coordinates.
- 🧪 **Omniscience Identification Table**: Decodes the true identities of unknown potion, scroll, and wand appearances (e.g. *purple-red potion ➜ potion of rage*), eliminating blind trial-and-error!
- 📦 **Sokoban Grand Prize Scout**: Peeks ahead at floor 4's "Big Three" magical rewards (Bag of Holding / Amulet of Life Saving / Cloak of Deflection).

### 3. ⚡ Ultra-Fast Multi-Criteria Seed Filtering (Filter / Roll)
- 🎯 **Target Artifact Search**: Search by name (supports both English and Chinese, exact or fuzzy).
- 🪜 **Depth & Region Constraints**: Restrict artifact generation to specific depths or areas (e.g. must appear on level 1 or 3).
- 📦 **Artifact Count Threshold**: Demand runs with at least $N$ artifacts (e.g., $\ge 20$ artifacts for high-drop runs).
- ⚔️ **Minetown Ecology Filter**: Filter seeds specifically for peaceful or orcish massacre states.
- 🚀 **High-Throughput Rolling**: With multi-core parallelism, sweeps hundreds of full seeds per second to find god-tier setups in moments!

---

## 🚀 Quick Start

### Method 1: One-Click Run (Recommended)

Double-click **`run.bat`** in the project root:
1. The program starts a lightweight local web service (listening on `http://127.0.0.1:5218`).
2. **Automatically opens the visual console in your default browser**!
3. On first run, simply enter your local *Pathos* installation path (e.g. `G:\games\Pathos` or your Steam folder) in the header bar and click save to start analyzing and rolling seeds.

---

### Method 2: Command Line & Terminal Modes (CLI)

For command-line enthusiasts, the tool provides comprehensive interactive terminal and automated parameter options:

#### 1. Interactive Terminal Console
```bash
cd PathosSeedFinder
dotnet run -- cli
```

#### 2. Analyze a Specific Seed
```bash
dotnet run -- analyze -s <seed_text_or_code>
```
*Examples:*
```bash
dotnet run -- analyze -s test1234
dotnet run -- analyze -s seed_77217
```

#### 3. Filter Seeds by Conditions
```bash
dotnet run -- filter [options]
```

| Option | Short | Description | Example |
| :--- | :--- | :--- | :--- |
| `--artifact <name>` | `-art` | Required artifact name (English or Chinese) | `--artifact Mirrorbright` |
| `--level <floor>` | `-l` | Floor depth where artifact must appear | `--level 1` or `--level 3` |
| `--site <branch>` | None | Name of the branch/site where artifact appears | `--site "Elf Kingdom"` |
| `--min-artifacts <count>` | `-m` | Minimum total artifacts in the run | `--min-artifacts 18` |
| `--minetown <state>` | `-mt` | Minetown state (`normal` / `orc`) | `--minetown orc` |
| `--max <limit>` | `-n` | Maximum seed search limit | `--max 500` |

*Examples:*
```bash
# Find seeds containing Mirrorbright on floor 1:
dotnet run -- filter --artifact Mirrorbright --level 1 --max 100

# Find seeds with an Orcish Massacre minetown:
dotnet run -- filter --minetown orc --max 100

# Find high-drop runs with at least 18 artifacts:
dotnet run -- filter --min-artifacts 18 --max 200
```

---

## 🛠️ Architecture & Building from Source

- **Tech Stack**: .NET 8.0 SDK / C# 12 / HTML5 Canvas
- **Simulation Mechanism**: Dynamically attaches the game's core engine libraries (`PathosEngine.dll`, `Inv.Library.dll`, `PathosOfficial.dll`) in isolated `AssemblyLoadContext` instances to achieve 100% genuine parity with native generation logic.
- **Custom Build**: If your Pathos installation is in a custom path, compile with MSBuild properties:
  ```bash
  dotnet build -p:PathosDir="D:\Your\Pathos\GamePath"
  ```
  *(Note: The assembly name is preserved as `PathosSyntaxTest` to satisfy internal accessibility constraints defined by the game's core engine).*

---

## 🤝 Acknowledgments

- **Game Original**: Sincere gratitude to the classic Roguelike masterpiece *Pathos: Nethack Codex* and its creator **Callan O'Neill** for the wonderful dungeon adventure experience.
- **Engineering Collaboration**: Conceived, architected, and verified by human developer, in deep collaboration with **Google Gemini (Google DeepMind)** for advanced code generation and engineering implementation.

---

## 📜 License & Legal Disclaimer

- This project is licensed under the [MIT License](LICENSE).
- **Disclaimer**: *Pathos: Nethack Codex* is the intellectual property of **Callan O'Neill**. This project is an independent third-party companion tool and does not distribute any copyrighted game binaries or proprietary assets. Users must own a legitimate copy of the game.
