// Pathos Seed Finder - Internationalization (i18n) Module
// Supports: Chinese (zh) and English (en)

(function () {
  'use strict';

  const STORAGE_KEY = 'pathos_seed_finder_lang';
  let currentLanguage = localStorage.getItem(STORAGE_KEY) || 'zh';
  if (currentLanguage !== 'zh' && currentLanguage !== 'en') {
    currentLanguage = 'zh';
  }

  const translations = {
    zh: {
      // Header & Navigation
      'app.title': 'Pathos Seed Finder',
      'app.version': 'WebGUI v1.0',
      'app.subtitle': '基于 Pathos: Nethack Codex 原生引擎反射模拟',
      'nav.analyze': '透视模式 (Analyze)',
      'nav.find': '筛选模式 (Find / Roll)',
      'nav.gamedir': '游戏目录',
      'nav.gamedir.unbound': '⚠️ 请设置游戏目录',
      'nav.gamedir.bound': '🎮 已挂载',
      'nav.themeToggle': '切换暗黑/明亮主题',
      'nav.langToggle': '切换语言 / Switch Language',

      // Settings Modal
      'settings.title': 'Pathos 游戏目录与版本设置',
      'settings.subtitle': '指定电脑上的游戏安装根目录，即可无缝适配您的游戏版本',
      'settings.mountStatus': '挂载状态:',
      'settings.checking': '正在检查...',
      'settings.unbound': '未绑定',
      'settings.unconfigured': '未配置 (请选择目录)',
      'settings.candidatesTitle': '🔍 自动检测到的游戏安装位置 (点击可直接填入)',
      'settings.candidatesScanning': '正在扫描常用安装路径...',
      'settings.candidatesNone': '未在常见安装路径下找到游戏，请点击“浏览文件夹”手动选择',
      'settings.manualTitle': '手动指定或浏览目录',
      'settings.placeholder': '例如: C:\\Games\\Pathos 或 Steam 路径',
      'settings.browse': '浏览文件夹',
      'settings.tip': '提示：必须包含 PathosEngine.dll、PathosOfficial.dll 以及 Assets/Pathos.Codex。',
      'settings.tilesetTitle': '🎨 游戏贴图风格 (Tileset)',
      'settings.tilesetDefault': '默认: Sonyvanda 精致原版',
      'settings.tilesetSonyvanda': 'Sonyvanda',
      'settings.tilesetSonyvandaDesc': '精致原版 (默认)',
      'settings.tilesetClassic': 'Classic',
      'settings.tilesetClassicDesc': '怀旧像素风',
      'settings.tilesetGeoduck': 'Geoduck',
      'settings.tilesetGeoduckDesc': '矢量平铺风',
      'settings.tilesetAbsurd': 'Absurd',
      'settings.tilesetAbsurdDesc': '高清手绘风',
      'settings.languageTitle': '🌐 界面语言 (Interface Language)',
      'settings.langZh': '简体中文 (Chinese)',
      'settings.langEn': 'English (英语)',
      'settings.cancel': '取消',
      'settings.save': '保存并初始化',

      // View 1: Analyze Mode
      'analyze.seedLabel': '种子:',
      'analyze.seedPlaceholder': '输入种子，如 #12345678 或 任意文字 (test1234)',
      'analyze.clear': '清空',
      'analyze.btnRun': '深度分析',
      'analyze.btnRandom': '随机',
      'analyze.btnCopy': '复制',
      'analyze.btnCopied': '已复制',
      'analyze.btnExport': '导出报告',
      'analyze.curSeed': '当前种子:',
      'analyze.elapsed': '解析耗时:',
      'analyze.artCount': '生成神器数:',
      'analyze.shopCount': '设施商店数:',
      'analyze.minetownTitle': '矿镇生态 (Minetown)',
      'analyze.minetownLoading': '分析中...',
      'analyze.minetownGuards': '警卫:',
      'analyze.minetownOrcs': '兽人:',
      'analyze.minetownMerchants': '设施:',
      'analyze.minetownPeaceful': '秩序井然 (Peaceful)',
      'analyze.minetownSiege': '兽人攻城 (Orcish Siege)',
      'analyze.minetownNone': '未生成',
      'analyze.minetownNoneDesc': '未检测到矿镇地图',
      'analyze.terrain': '地貌:',
      'analyze.terrainWater': '🌊 地下水体',
      'analyze.terrainLava': '🌋 地下岩浆',
      'analyze.elfTitle': '精灵王国 (Elf Kingdom)',
      'analyze.elfLoading': '分析中...',
      'analyze.elfTown': '城内精灵:',
      'analyze.elfPalace': '王宫守备:',
      'analyze.elfEntrance': '入口:',
      'analyze.elfPeaceful': '盛世繁荣 (Peaceful)',
      'analyze.elfRebellion': '政变暴动 (Rebellion)',
      'analyze.elfNone': '未生成',
      'analyze.elfNoneDesc': '未检测到精灵王国地图',
      'analyze.sokobanTitle': '仓库番大奖 (Sokoban)',
      'analyze.sokobanEntrance': '入口: ',
      'analyze.sokobanAnalyzing': '正在分析仓库番大奖...',
      'analyze.sokobanFloorsHint': '1~3层沿途魔法道具已分析',
      'analyze.sokobanNone': '未生成',
      'analyze.sokobanNoRewards': '暂无第4层专属大奖',

      // Subtabs in Analyze Mode
      'subtab.artifacts': '🗡️ 全神器精准定位',
      'subtab.identities': '🧪 全药水卷轴鉴定',
      'subtab.map': '🗺️ 全层地图透视',
      'subtab.shops': '🏪 商店与商人生态',
      'subtab.sokoban': '📦 仓库番沿途道具明细',

      // Artifacts Filter & Table
      'art.filterPlaceholder': '搜索神器中文/英文名、区域、持有者...',
      'art.siteAll': '全部',
      'art.siteDungeon': '主地牢',
      'art.siteMines': '矿洞',
      'art.siteUnderdeep': '地城',
      'art.siteElf': '精灵王国',
      'art.siteAbyss': '深渊',
      'art.thName': '神器名称 (Name)',
      'art.thLocation': '所在区域与楼层 (Location)',
      'art.thDungeonLevel': '主地牢折算层',
      'art.thPossession': '存放形态 (Possession)',
      'art.thHolder': '藏匿者 / 容器详情',
      'art.thCoords': '坐标 (X, Y)',
      'art.empty': '没有符合筛选条件的神器',
      'art.analyzing': '正在分析神器列表...',
      'art.possessionGround': '地面散落',
      'art.possessionContainer': '箱子容器',
      'art.possessionCarried': '角色携带',
      'art.possessionCovered': '巨石压覆',
      'art.flagLocked': ' [上锁]',
      'art.flagTrapped': ' [陷阱]',
      'art.dungeonLevelFormat': '地牢 第 {0} 层',

      // Identities Filter & Table
      'id.filterPlaceholder': '搜索真实名称、未鉴定外观 (如 灿蓝、治疗、PRATYAVAYAH)...',
      'id.seriesAll': '全部',
      'id.seriesPotion': '🧪 药水',
      'id.seriesScroll': '📜 卷轴',
      'id.seriesWand': '🪄 魔杖',
      'id.seriesRing': '💍 戒指',
      'id.seriesAmulet': '📿 护符',
      'id.seriesBook': '📖 技能书',
      'id.seriesGear': '🛡️ 装备/其它',
      'id.hint': '💡 本表显示的是当前种子「未经过开局重掷（Reroll）」时的初始药水、卷轴、魔杖、戒指等外观对应表。',
      'id.rerollWarningTitle': '开局重掷（Reroll）提示：',
      'id.rerollWarningBody': '在游戏开局预览界面点击“重掷角色（Reroll）”刷属性/技能时，官方引擎会按系统时间重新洗牌所有未鉴定道具的外观，导致本表失效。但地图布局、神器位置以及【全层地图透视】中每个坐标点上道具的真实种类均不会改变——对照透视地图上的实际坐标拾取即可轻松反推重掷后的新外观！',
      'id.countHint': '共 {0} 项道具，当前显示 {1} 项',
      'id.thCategory': '类别 (Type)',
      'id.thApparent': '未鉴定外观 (Game Appearance)',
      'id.thArrow': '对应',
      'id.thReal': '真实身份 (True Identity)',
      'id.thCode': '英文代码 (Codex Name)',
      'id.empty': '没有符合筛选条件的道具',
      'id.analyzing': '正在分析全药水卷轴鉴定表...',

      // Map Viewer
      'map.selectFloor': '选择楼层:',
      'map.loadingFloors': '正在加载楼层列表...',
      'map.gridUnit': '网格',
      'map.cursor': '指针:',
      'map.tileset': '贴图包:',
      'map.layerTextures': '🎨 原版贴图',
      'map.layerItems': '📦 道具分布',
      'map.layerShops': '🏪 商店/商人',
      'map.layerBoulders': '🪨 巨石',
      'map.layerTraps': '⚠️ 陷阱',
      'map.layerStairs': '🚪 楼梯高亮',
      'map.layerFixtures': '✨ 设施贴图',
      'map.layerSecrets': '👁️ 隐藏暗门',
      'map.btnZoomIn': '放大',
      'map.btnZoomOut': '缩小',
      'map.btnCenter': '🎯 居中',
      'map.rendering': '正在渲染楼层全息地图...',
      'map.helperNote': '🖱️ 鼠标滚轮：缩放画布 | 按住左键拖拽：平移地图 | 鼠标悬停：探查地块信息',

      // Shops Tab
      'shops.tip': '⭐ 前期关键商业区（主地牢 1~10 层、矿镇 1 层）的物资对于前期平稳破局至关重要！',
      'shops.countBadge': '共 {0} 处设施',
      'shops.filteredHint': '当前显示 {0} / {1} 处',
      'shops.filterPlaceholder': '搜索商店/设施、店主或区域 (如 祭坛、制作站点、古兵器、神秘据点)...',
      'shops.dungeonOnly': '🏰 仅看主地牢',
      'shops.resetFilter': '✕ 显示全部',
      'shops.earliestTitle': '⚡ 各类商店 / 设施最早出现速查（点击筛选，点 📍 直达透视地图）',
      'shops.earliestBadge': '🏆 最早出现',
      'shops.jumpMap': '📍 透视',
      'shops.empty': '未检测到符合条件的商店或设施',
      'shops.loading': '正在加载商店数据...',
      'shops.keeper': '店主:',
      'shops.noKeeper': '无守备店主',

      // Sokoban Tab
      'soko.loading': '正在加载仓库番详情...',
      'soko.empty': '未检测到仓库番',
      'soko.big3Title': '🏆 第 4 层终极宝藏三大件 (Sokoban Big 3)',
      'soko.bag': '魔法背包:',
      'soko.cloak': '魔法斗篷:',
      'soko.amulet': '魔法护符:',
      'soko.none': '无',
      'soko.rewardsTitle': '📜 1~3 层沿途魔法道具 ({0} 件)',
      'soko.floorFormat': '第 {0} 层',

      // View 2: Find Mode
      'find.title': '🎯 复合过滤条件配置',
      'find.subtitle': '支持自由添加神器、层数、矿镇、精灵等多个联动条件',
      'find.clear': '清空',
      'find.logicMode': '过滤判定逻辑:',
      'find.matchAll': '全部满足 (Match All)',
      'find.matchAny': '任一满足 (Match Any)',
      'find.presetsTitle': '⚡ 预设',
      'find.presetHighArt': '🏆 神器大满贯 (>=18件)',
      'find.btnSavePreset': '存为预设',
      'find.promptPresetName': '请输入预设名称:',
      'find.presetSaved': '已保存预设: {0}',
      'find.presetDeleted': '已删除预设: {0}',
      'find.presetRenamed': '已重命名预设: {0}',
      'find.noRulesToSave': '当前没有激活的过滤条件，请先添加条件再保存！',
      'find.activeRulesTitle': '已激活的过滤条件',
      'find.activeRulesHint': '点击下方按钮可继续添加',
      'find.btnAddRule': '➕ 添加一个新过滤条件 (Add Condition)',
      'find.scaleTitle': '⚙️ 检索规模与范围',
      'find.maxSeeds': '测试种子数',
      'find.startSeed': '起始种子 (留空随机)',
      'find.startSeedPlaceholder': '随机起始',
      'find.threadsTitle': '⚡ 多线程并发规模 (Worker Threads)',
      'find.threadsFormat': '{0} 线程并发',
      'find.threadsNote': '每个线程独占独立游戏引擎上下文，多核算力全开，速度提升数倍！',
      'find.btnStart': '开始检索种子',
      'find.btnStop': '停止',
      'find.readyText': '就绪，请配置左侧条件后检索',
      'find.matchesCount': '已命中: {0} 种子',
      'find.clearResults': '清空',
      'find.statTested': '已测试',
      'find.statMatched': '命中数',
      'find.statSpeed': '速度',
      'find.statCurrent': '当前扫描',
      'find.speedUnit': '种/s',
      'find.matchListTitle': '匹配种子列表',
      'find.matchListHint': '点击行即可右侧即时透视',
      'find.matchEmptyTitle': '配置左侧条件后开始检索',
      'find.matchEmptyDesc': '命中种子会即时在此列出',
      'find.previewPlaceholderTitle': '选择左侧任意种子查看全息速览',
      'find.previewPlaceholderDesc': '无需跳转页面，同屏即时透视高亮命中项与楼层物资',
      'find.previewSelected': '当前选中:',
      'find.previewCopy': '📋 复制',
      'find.previewCopied': '✓ 已复制',
      'find.previewAnalyze': '🔍 深入透视',
      'find.previewVerifyTitle': '🎯 命中条件核验:',
      'find.previewAllMatched': '✓ 完全符合全部自定义检索条件',
      'find.previewMinetown': '🏰 矿镇生态',
      'find.previewElf': '🧝 精灵王国',
      'find.previewSokoban': '📦 仓库番入口',
      'find.previewEarlyArt': '🗡️ 前期关键神器分布 (前 10 层):',
      'find.previewEarlyCount': '前期共 {0} 件',
      'find.previewEarlyEmpty': '前 10 层未生成预置神器（神器集中在深层地牢）',
      'find.rulesEmpty': '暂无过滤条件（将检索任意种子），点击下方“➕ 添加条件”即可自由组合！',
      'find.mixedModeHint': '包含备选池: 必选全中 + 备选任一',

      // Rule item dynamic texts
      'rule.roleMust': '必选',
      'rule.roleOptional': '备选',
      'rule.roleMustTip': '【必选条件】此条件必须满足 (AND)',
      'rule.roleOptionalTip': '【备选条件】所有备选条件中只需命中任意一个即可 (OR)',
      'rule.typeArtifact': '🗡️ 道具/神器约束',
      'rule.typeMinetown': '🏰 矿镇生态状态',
      'rule.typeElf': '🧝 精灵王国状态',
      'rule.typeSokoban': '📦 仓库番大奖需求',
      'rule.typeShop': '🏪 商业设施需求',
      'rule.typeArtifactCount': '🏆 全局神器数总额',
      'rule.allowCarriedLabel': '允许怪物携带 (Boss/生物掉落)',
      'rule.allowCarriedTip': '取消勾选后，仅筛选地面散落、宝箱或巨石掩埋的神器，避免前期打不过 Boss 导致无法获取',
      'rule.artNameLabel': '指定神器名称 (支持中英文模糊搜索)',
      'rule.artNamePlaceholder': '输入汉字或英文，如: 明, Mirror...',
      'rule.maxLevelLabel': '出现层数 (<= N 层)',
      'rule.maxLevelPlaceholder': '如: 3',
      'rule.siteLabel': '限制区域',
      'rule.siteAny': '不限区域',
      'rule.siteDungeon': '主地牢 (Dungeon)',
      'rule.siteMines': '矿洞 (Mines)',
      'rule.siteElf': '精灵王国 (Elf Kingdom)',
      'rule.siteSokoban': '仓库番 (Sokoban)',
      'rule.siteLudios': '卢底人堡垒 (Fort Ludios)',
      'rule.minetownStateLabel': '矿镇必须处于的状态',
      'rule.minetownStateOrc': '⚔️ 兽人屠城 / 攻城 (混乱战场)',
      'rule.minetownStateNormal': '🛡️ 守卫巡逻 / 秩序井然 (和平城镇)',
      'rule.elfStateLabel': '精灵王国必须处于的状态',
      'rule.elfStateMassacre': '🔥 屠城惨案 / 敌对暴动 (高烈度)',
      'rule.elfStatePeaceful': '🌿 盛世繁荣 / 友好和平 (宁静王国)',
      'rule.sokoPrizeLabel': '指定仓库番奖励大奖 (支持模糊匹配)',
      'rule.sokoPrizePlaceholder': '如: 收纳之袋, 反射护符...',
      'rule.quickFill': '快捷填写:',
      'rule.shopTypeLabel': '指定商业/特殊设施类型 (支持中英文模糊匹配)',
      'rule.shopTypePlaceholder': '如: 制作站点, 祭坛, 珠宝店, craft station...',
      'rule.minShopsLabel': '最少设施总数 (>= N)',
      'rule.minArtifactsLabel': '全局神器最少生成总数 (>= N 件)',
      'rule.minArtifactsTip': 'Pathos 标准地牢平均生成 12~15 件神器，>=18 件即为顶级高爆种子。',

      // Dynamic Toast & Alert Messages
      'toast.analyzing': '正在深度分析种子...',
      'toast.analyzeDone': '种子分析完成！耗时: {0} ms',
      'toast.copySuccess': '种子已复制到剪贴板！',
      'toast.searchStarted': '已启动高速检索工作池...',
      'toast.searchStopped': '检索已停止',
      'toast.searchDone': '检索完毕！共命中 {0} 个种子',
      'toast.needDir': '请先指定有效的 Pathos 游戏目录以初始化核心算法！'
    },

    en: {
      // Header & Navigation
      'app.title': 'Pathos Seed Finder',
      'app.version': 'WebGUI v1.0',
      'app.subtitle': 'Pathos: Nethack Codex Native Engine Simulation',
      'nav.analyze': 'Analyze Mode',
      'nav.find': 'Find / Roll Mode',
      'nav.gamedir': 'Game Directory',
      'nav.gamedir.unbound': '⚠️ Configure Path',
      'nav.gamedir.bound': '🎮 Mounted',
      'nav.themeToggle': 'Toggle Dark/Light Theme',
      'nav.langToggle': 'Language / 切换语言',

      // Settings Modal
      'settings.title': 'Pathos Game Directory & Version Settings',
      'settings.subtitle': 'Specify the game installation root directory to seamlessly match your game version',
      'settings.mountStatus': 'Mount Status:',
      'settings.checking': 'Checking...',
      'settings.unbound': 'Not bound',
      'settings.unconfigured': 'Not configured (Please select path)',
      'settings.candidatesTitle': '🔍 Auto-detected Game Locations (Click to fill)',
      'settings.candidatesScanning': 'Scanning common installation paths...',
      'settings.candidatesNone': 'No game directory found in common locations. Click "Browse" to select manually.',
      'settings.manualTitle': 'Manual Path or Browse',
      'settings.placeholder': 'e.g. C:\\Games\\Pathos or Steam path',
      'settings.browse': 'Browse Folder',
      'settings.tip': 'Note: Must contain PathosEngine.dll, PathosOfficial.dll and Assets/Pathos.Codex.',
      'settings.tilesetTitle': '🎨 Tileset Style',
      'settings.tilesetDefault': 'Default: Sonyvanda Classic Original',
      'settings.tilesetSonyvanda': 'Sonyvanda',
      'settings.tilesetSonyvandaDesc': 'Refined Original (Default)',
      'settings.tilesetClassic': 'Classic',
      'settings.tilesetClassicDesc': 'Retro Pixel Art',
      'settings.tilesetGeoduck': 'Geoduck',
      'settings.tilesetGeoduckDesc': 'Clean Vector Art',
      'settings.tilesetAbsurd': 'Absurd',
      'settings.tilesetAbsurdDesc': 'HD Hand-drawn',
      'settings.languageTitle': '🌐 Interface Language',
      'settings.langZh': '简体中文 (Chinese)',
      'settings.langEn': 'English',
      'settings.cancel': 'Cancel',
      'settings.save': 'Save & Initialize',

      // View 1: Analyze Mode
      'analyze.seedLabel': 'Seed:',
      'analyze.seedPlaceholder': 'Enter seed code or any text (e.g. 20240901, test1234)',
      'analyze.clear': 'Clear',
      'analyze.btnRun': 'Analyze Seed',
      'analyze.btnRandom': 'Random',
      'analyze.btnCopy': 'Copy',
      'analyze.btnCopied': 'Copied',
      'analyze.btnExport': 'Export Report',
      'analyze.curSeed': 'Active Seed:',
      'analyze.elapsed': 'Elapsed Time:',
      'analyze.artCount': 'Total Artifacts:',
      'analyze.shopCount': 'Total Facilities:',
      'analyze.minetownTitle': 'Minetown Ecology',
      'analyze.minetownLoading': 'Analyzing...',
      'analyze.minetownGuards': 'Watchmen:',
      'analyze.minetownOrcs': 'Orcs:',
      'analyze.minetownMerchants': 'Facilities:',
      'analyze.minetownPeaceful': 'Peaceful',
      'analyze.minetownSiege': 'Orcish Siege',
      'analyze.minetownNone': 'Not Generated',
      'analyze.minetownNoneDesc': 'Minetown map not found in this seed',
      'analyze.terrain': 'Terrain:',
      'analyze.terrainWater': '🌊 Underground Water',
      'analyze.terrainLava': '🌋 Underground Lava',
      'analyze.elfTitle': 'Elf Kingdom',
      'analyze.elfLoading': 'Analyzing...',
      'analyze.elfTown': 'Town Elves:',
      'analyze.elfPalace': 'Palace Guards:',
      'analyze.elfEntrance': 'Entrance:',
      'analyze.elfPeaceful': 'Peaceful & Prosperous',
      'analyze.elfRebellion': 'Rebellion / Massacre',
      'analyze.elfNone': 'Not Generated',
      'analyze.elfNoneDesc': 'Elf Kingdom map not found in this seed',
      'analyze.sokobanTitle': 'Sokoban Overview',
      'analyze.sokobanEntrance': 'Entrance: ',
      'analyze.sokobanAnalyzing': 'Analyzing Sokoban prizes...',
      'analyze.sokobanFloorsHint': 'Floors 1~3 items analyzed',
      'analyze.sokobanNone': 'Not Generated',
      'analyze.sokobanNoRewards': 'No Level 4 exclusive rewards',

      // Subtabs in Analyze Mode
      'subtab.artifacts': '🗡️ Artifact Locations',
      'subtab.identities': '🧪 Pre-Identified Items',
      'subtab.map': '🗺️ Map Visualizer',
      'subtab.shops': '🏪 Shops & Facilities',
      'subtab.sokoban': '📦 Sokoban Items & Solutions',

      // Artifacts Filter & Table
      'art.filterPlaceholder': 'Filter by name, site, holder...',
      'art.siteAll': 'All Sites',
      'art.siteDungeon': 'Dungeon',
      'art.siteMines': 'Mines',
      'art.siteUnderdeep': 'Underdeep',
      'art.siteElf': 'Elf Kingdom',
      'art.siteAbyss': 'Abyss',
      'art.thName': 'Artifact Name',
      'art.thLocation': 'Location & Floor',
      'art.thDungeonLevel': 'Dungeon Level',
      'art.thPossession': 'State',
      'art.thHolder': 'Holder / Container',
      'art.thCoords': 'Coords (X, Y)',
      'art.empty': 'No artifacts match the filter criteria',
      'art.analyzing': 'Analyzing artifacts list...',
      'art.possessionGround': 'Ground',
      'art.possessionContainer': 'Container',
      'art.possessionCarried': 'Carried',
      'art.possessionCovered': 'Under Boulder',
      'art.flagLocked': ' [Locked]',
      'art.flagTrapped': ' [Trapped]',
      'art.dungeonLevelFormat': 'Dungeon Level {0}',

      // Identities Filter & Table
      'id.filterPlaceholder': 'Search real name or game appearance (e.g. ruby, healing, ELAM EBOW)...',
      'id.seriesAll': 'All',
      'id.seriesPotion': '🧪 Potions',
      'id.seriesScroll': '📜 Scrolls',
      'id.seriesWand': '🪄 Wands',
      'id.seriesRing': '💍 Rings',
      'id.seriesAmulet': '📿 Amulets',
      'id.seriesBook': '📖 Books',
      'id.seriesGear': '🛡️ Equipment',
      'id.hint': '💡 This table shows the initial (non-rerolled) unidentified appearance mapping for this seed.',
      'id.rerollWarningTitle': 'Character Reroll Warning:',
      'id.rerollWarningBody': 'Clicking "Reroll" on the in-game character confirmation screen causes the game engine to re-shuffle all unidentified appearances using system time, invalidating this table. However, dungeon layouts, artifact placements, and the true item identities at every coordinate in the Map Visualizer NEVER change—simply check an item at its map coordinate to deduce its new rerolled appearance!',
      'id.countHint': '{0} items total, {1} displayed',
      'id.thCategory': 'Type',
      'id.thApparent': 'Unidentified Appearance',
      'id.thArrow': 'Is',
      'id.thReal': 'True Identity',
      'id.thCode': 'Codex Term',
      'id.empty': 'No items match the filter criteria',
      'id.analyzing': 'Analyzing item identification table...',

      // Map Viewer
      'map.selectFloor': 'Floor:',
      'map.loadingFloors': 'Loading floors...',
      'map.gridUnit': 'grid',
      'map.cursor': 'Cursor:',
      'map.tileset': 'Tileset:',
      'map.layerTextures': '🎨 Textures',
      'map.layerItems': '📦 Items',
      'map.layerShops': '🏪 Shops',
      'map.layerBoulders': '🪨 Boulders',
      'map.layerTraps': '⚠️ Traps',
      'map.layerStairs': '🚪 Stairs',
      'map.layerFixtures': '✨ Fixtures',
      'map.layerSecrets': '👁️ Secret Doors',
      'map.btnZoomIn': 'Zoom In',
      'map.btnZoomOut': 'Zoom Out',
      'map.btnCenter': '🎯 Fit',
      'map.rendering': 'Rendering floor map...',
      'map.helperNote': '🖱️ Mouse Wheel: Zoom | Left Click Drag: Pan | Hover: Inspect tile',

      // Shops Tab
      'shops.tip': '⭐ Early facilities (Dungeon 1~10, Minetown 1) provide crucial supplies for survival!',
      'shops.countBadge': '{0} facilities total',
      'shops.filteredHint': 'Showing {0} / {1}',
      'shops.filterPlaceholder': 'Search shop/facility, keeper, or site (e.g. shrine, craft, antique)...',
      'shops.dungeonOnly': '🏰 Main Dungeon Only',
      'shops.resetFilter': '✕ Show All',
      'shops.earliestTitle': '⚡ Earliest Appearance by Type (Click to filter, click 📍 to jump to map)',
      'shops.earliestBadge': '🏆 Earliest',
      'shops.jumpMap': '📍 Map',
      'shops.empty': 'No matching shops or facilities found',
      'shops.loading': 'Loading shop data...',
      'shops.keeper': 'Keeper:',
      'shops.noKeeper': 'Unattended',

      // Sokoban Tab
      'soko.loading': 'Loading Sokoban details...',
      'soko.empty': 'No Sokoban branch found',
      'soko.big3Title': '🏆 Floor 4 Big 3 Ultimate Rewards',
      'soko.bag': 'Bag:',
      'soko.cloak': 'Cloak:',
      'soko.amulet': 'Amulet:',
      'soko.none': 'None',
      'soko.rewardsTitle': '📜 Floors 1~3 Along-the-Way Items ({0} items)',
      'soko.floorFormat': 'Floor {0}',

      // View 2: Find Mode
      'find.title': '🎯 Composite Filter Rules',
      'find.subtitle': 'Freely combine artifact, floor, Minetown, and Elf Kingdom criteria',
      'find.clear': 'Clear',
      'find.logicMode': 'Match Logic:',
      'find.matchAll': 'Match All (AND)',
      'find.matchAny': 'Match Any (OR)',
      'find.presetsTitle': '⚡ Presets',
      'find.presetHighArt': '🏆 Grand Slam (>=18 Artifacts)',
      'find.btnSavePreset': 'Save Preset',
      'find.promptPresetName': 'Enter preset name:',
      'find.presetSaved': 'Preset saved: {0}',
      'find.presetDeleted': 'Preset deleted: {0}',
      'find.presetRenamed': 'Preset renamed: {0}',
      'find.noRulesToSave': 'No active rules to save as preset!',
      'find.activeRulesTitle': 'Active Conditions',
      'find.activeRulesHint': 'Click button below to add more',
      'find.btnAddRule': '➕ Add Condition Rule',
      'find.scaleTitle': '⚙️ Search Scale & Range',
      'find.maxSeeds': 'Seeds to Test',
      'find.startSeed': 'Start Seed (blank for random)',
      'find.startSeedPlaceholder': 'Random start',
      'find.threadsTitle': '⚡ Concurrency (Worker Threads)',
      'find.threadsFormat': '{0} Threads',
      'find.threadsNote': 'Each thread runs an independent game engine context for maximum throughput!',
      'find.btnStart': 'Start Search',
      'find.btnStop': 'Stop',
      'find.readyText': 'Ready. Configure filters on the left to begin.',
      'find.matchesCount': 'Matches: {0} seeds',
      'find.clearResults': 'Clear',
      'find.statTested': 'Tested',
      'find.statMatched': 'Matches',
      'find.statSpeed': 'Speed',
      'find.statCurrent': 'Scanning',
      'find.speedUnit': 'seeds/s',
      'find.matchListTitle': 'Matched Seeds',
      'find.matchListHint': 'Click any row to preview on the right',
      'find.matchEmptyTitle': 'Configure filters to start search',
      'find.matchEmptyDesc': 'Matching seeds will appear here in real-time',
      'find.previewPlaceholderTitle': 'Select any seed to inspect preview',
      'find.previewPlaceholderDesc': 'Inspect early artifacts and macro ecology on the same screen',
      'find.previewSelected': 'Selected Seed:',
      'find.previewCopy': '📋 Copy',
      'find.previewCopied': '✓ Copied',
      'find.previewAnalyze': '🔍 Open in Analyzer',
      'find.previewVerifyTitle': '🎯 Matched Conditions:',
      'find.previewAllMatched': '✓ Satisfies all custom search criteria',
      'find.previewMinetown': '🏰 Minetown',
      'find.previewElf': '🧝 Elf Kingdom',
      'find.previewSokoban': '📦 Sokoban Entrance',
      'find.previewEarlyArt': '🗡️ Early Artifacts (Floors 1~10):',
      'find.previewEarlyCount': '{0} early items',
      'find.previewEarlyEmpty': 'No preset artifacts generated in top 10 floors',
      'find.rulesEmpty': 'No filter rules active (will match any seed). Click "➕ Add Condition Rule" below!',
      'find.mixedModeHint': 'Mixed: All MUST + Any OPTIONAL',

      // Rule item dynamic texts
      'rule.roleMust': 'MUST',
      'rule.roleOptional': 'ANY',
      'rule.roleMustTip': '[Must] This condition must be met (AND)',
      'rule.roleOptionalTip': '[Optional] At least one of the optional conditions must be met (OR)',
      'rule.typeArtifact': '🗡️ Item / Artifact Constraint',
      'rule.typeMinetown': '🏰 Minetown Ecology State',
      'rule.typeElf': '🧝 Elf Kingdom State',
      'rule.typeSokoban': '📦 Sokoban Reward Requirement',
      'rule.typeShop': '🏪 Commercial Facilities Requirement',
      'rule.typeArtifactCount': '🏆 Global Artifacts Count',
      'rule.allowCarriedLabel': 'Allow carried by monsters (Boss/Creature)',
      'rule.allowCarriedTip': 'Uncheck to only match items on ground, in containers, or under boulders, avoiding tough bosses.',
      'rule.artNameLabel': 'Artifact Name (English or Chinese)',
      'rule.artNamePlaceholder': 'e.g. Mirrorbright, Excalibur...',
      'rule.maxLevelLabel': 'Max Dungeon Level (<= N)',
      'rule.maxLevelPlaceholder': 'e.g. 3',
      'rule.siteLabel': 'Restricted Site',
      'rule.siteAny': 'Any Site',
      'rule.siteDungeon': 'Dungeon Only',
      'rule.siteMines': 'Mines Only',
      'rule.siteElf': 'Elf Kingdom Only',
      'rule.siteSokoban': 'Sokoban Only',
      'rule.siteLudios': 'Fort Ludios Only',
      'rule.minetownStateLabel': 'Required Minetown State',
      'rule.minetownStateOrc': '⚔️ Orcish Siege (Chaotic Battle)',
      'rule.minetownStateNormal': '🛡️ Watchmen Patrol (Peaceful Town)',
      'rule.elfStateLabel': 'Required Elf Kingdom State',
      'rule.elfStateMassacre': '🔥 Rebellion / Massacre (Hostile)',
      'rule.elfStatePeaceful': '🌿 Prosperous & Peaceful (Friendly)',
      'rule.sokoPrizeLabel': 'Required Sokoban Reward',
      'rule.sokoPrizePlaceholder': 'e.g. bag of holding, amulet of reflection...',
      'rule.quickFill': 'Quick Fill:',
      'rule.shopTypeLabel': 'Facility / Shop Type (English or Chinese)',
      'rule.shopTypePlaceholder': 'e.g. craft station, holy shrine, jewellers...',
      'rule.minShopsLabel': 'Minimum Total Facilities (>= N)',
      'rule.minArtifactsLabel': 'Minimum Total Artifacts (>= N)',
      'rule.minArtifactsTip': 'Standard Pathos seeds average 12~15 artifacts. >=18 is a god seed.',

      // Dynamic Toast & Alert Messages
      'toast.analyzing': 'Analyzing seed with native engine...',
      'toast.analyzeDone': 'Seed analysis complete! Elapsed: {0} ms',
      'toast.copySuccess': 'Seed copied to clipboard!',
      'toast.searchStarted': 'Multi-threaded worker pool started...',
      'toast.searchStopped': 'Search stopped',
      'toast.searchDone': 'Search complete! Found {0} matching seeds',
      'toast.needDir': 'Please specify a valid Pathos game directory first!'
    }
  };

  function getLanguage() {
    return currentLanguage;
  }

  function t(key, ...args) {
    const langDict = translations[currentLanguage] || translations.zh;
    let val = langDict[key] ?? translations.zh[key] ?? key;
    if (args.length > 0) {
      args.forEach((arg, i) => {
        val = val.replace(new RegExp(`\\{${i}\\}`, 'g'), arg);
      });
    }
    return val;
  }

  // Formatters for game entities
  function formatArtifactName(art) {
    if (!art) return '';
    if (currentLanguage === 'en') {
      return art.englishName || art.chineseName || '';
    }
    return art.chineseName || art.englishName || '';
  }

  function formatLocation(site, siteChinese, levelIndex, dungeonLevel, subArea) {
    const s = (currentLanguage === 'en') ? (site || siteChinese) : (siteChinese || site);
    const sub = subArea ? ` [${subArea}]` : '';
    if (currentLanguage === 'en') {
      if (!site || site.toLowerCase() === 'dungeon') {
        return `${s} Level ${levelIndex}${sub}`;
      }
      return `${s} Level ${levelIndex}${sub} (Dungeon L${dungeonLevel})`;
    } else {
      if (!site || site.toLowerCase() === 'dungeon') {
        return `${s} 第${levelIndex}层${sub}`;
      }
      return `${s} 第${levelIndex}层${sub} [折算主地牢第${dungeonLevel}层]`;
    }
  }

  function formatHolder(holderName, holderChinese) {
    if (currentLanguage === 'en') {
      return holderName || holderChinese || '-';
    }
    if (holderChinese) {
      return `${holderChinese} <span class="text-slate-500 font-mono">(${holderName || ''})</span>`;
    }
    return holderName || '-';
  }

  function formatPossessionBadge(possession, isLocked, isTrapped) {
    const isEn = (currentLanguage === 'en');
    if (possession === 0) {
      return `<span class="px-2 py-0.5 rounded text-xs badge-possession-ground">${isEn ? 'Ground' : '地面散落'}</span>`;
    }
    if (possession === 1) {
      let flags = '';
      if (isLocked) flags += isEn ? ' [Locked]' : ' [上锁]';
      if (isTrapped) flags += isEn ? ' [Trapped]' : ' [陷阱]';
      return `<span class="px-2 py-0.5 rounded text-xs badge-possession-container">${isEn ? 'Container' : '箱子容器'}${flags}</span>`;
    }
    if (possession === 2) {
      return `<span class="px-2 py-0.5 rounded text-xs badge-possession-carried">${isEn ? 'Carried' : '角色携带'}</span>`;
    }
    return `<span class="px-2 py-0.5 rounded text-xs badge-possession-covered">${isEn ? 'Covered' : '巨石压覆'}</span>`;
  }

  function formatShopSpecialty(s) {
    if (!s) return '';
    if (currentLanguage === 'en') {
      return s.englishType || s.chineseType || '';
    }
    return s.chineseType || s.englishType || '';
  }

  function formatShopSubtype(s) {
    if (!s) return '';
    if (currentLanguage === 'en') {
      return s.chineseType || '';
    }
    return s.englishType || '';
  }

  function formatMerchant(merchantName) {
    if (!merchantName) {
      return currentLanguage === 'en' ? 'Unattended' : '无守备店主';
    }
    return merchantName;
  }

  function formatItemTrueName(item) {
    if (!item) return '';
    if (currentLanguage === 'en') {
      return item.trueNameEn || item.trueNameCn || '';
    }
    return item.trueNameCn || item.trueNameEn || '';
  }

  function formatItemApparentName(item) {
    if (!item) return '';
    if (currentLanguage === 'en') {
      return item.apparentNameEn || item.apparentNameCn || '';
    }
    return item.apparentNameCn || item.apparentNameEn || '';
  }

  function formatSeriesName(item) {
    if (!item) return '';
    if (currentLanguage === 'en') {
      const map = {
        potion: 'Potion',
        scroll: 'Scroll',
        wand: 'Wand',
        ring: 'Ring',
        amulet: 'Amulet',
        book: 'Spellbook',
        gear: 'Gear'
      };
      return map[item.series] || item.series || 'Item';
    }
    return item.seriesChinese || item.series || '道具';
  }

  function updateDOM() {
    // 1. Update text nodes
    document.querySelectorAll('[data-i18n]').forEach(el => {
      const key = el.getAttribute('data-i18n');
      if (key) {
        const text = t(key);
        if (el.children.length === 0) {
          el.textContent = text;
        } else {
          // If element has nested icons or spans, keep them if data-i18n-target is set,
          // or replace only text node
          const target = el.querySelector('[data-i18n-text]') || el;
          target.textContent = text;
        }
      }
    });

    // 2. Update placeholders
    document.querySelectorAll('[data-i18n-placeholder]').forEach(el => {
      const key = el.getAttribute('data-i18n-placeholder');
      if (key) {
        el.placeholder = t(key);
      }
    });

    // 3. Update titles / tooltips
    document.querySelectorAll('[data-i18n-title]').forEach(el => {
      const key = el.getAttribute('data-i18n-title');
      if (key) {
        el.title = t(key);
      }
    });

    // 4. Update html lang attribute
    document.documentElement.lang = currentLanguage === 'zh' ? 'zh-CN' : 'en';

    // 5. Update header lang toggle button text
    const headerLangBtn = document.getElementById('header-lang-btn');
    if (headerLangBtn) {
      headerLangBtn.innerHTML = currentLanguage === 'zh' ? '🌐 <b>中</b>' : '🌐 <b>EN</b>';
      headerLangBtn.title = t('nav.langToggle');
    }

    // 6. Sync settings modal radio buttons
    const langRadios = document.querySelectorAll('input[name="settings-language"]');
    langRadios.forEach(radio => {
      radio.checked = (radio.value === currentLanguage);
    });
    updateLanguageOptionsUI();

    const langIndicator = document.getElementById('settings-lang-indicator');
    if (langIndicator) {
      langIndicator.textContent = currentLanguage === 'zh' ? '简体中文' : 'English';
    }
  }

  function updateLanguageOptionsUI() {
    document.querySelectorAll('.language-option').forEach(label => {
      const radio = label.querySelector('input[type="radio"]');
      if (radio && radio.checked) {
        label.classList.add('selected', 'border-brand-500', 'bg-brand-500/10');
        label.classList.remove('border-surface-700');
      } else {
        label.classList.remove('selected', 'border-brand-500', 'bg-brand-500/10');
        label.classList.add('border-surface-700');
      }
    });
  }

  function setLanguage(lang, persist = true) {
    if (lang !== 'zh' && lang !== 'en') lang = 'zh';
    currentLanguage = lang;
    if (persist) {
      localStorage.setItem(STORAGE_KEY, lang);
    }
    updateDOM();

    // Dispatch custom event for app.js to re-render active report
    window.dispatchEvent(new CustomEvent('pathos:languageChanged', { detail: { language: lang } }));
  }

  function isZh() {
    return currentLanguage === 'zh';
  }

  function escapeHtml(str) {
    if (str == null) return '';
    return String(str)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;');
  }

  // Export to window
  window.I18N = {
    getLanguage,
    setLanguage,
    isZh,
    escapeHtml,
    t,
    formatArtifactName,
    formatLocation,
    formatHolder,
    formatPossessionBadge,
    formatShopSpecialty,
    formatShopSubtype,
    formatMerchant,
    formatItemTrueName,
    formatItemApparentName,
    formatSeriesName,
    updateDOM,
    updateLanguageOptionsUI
  };

  // Run DOM update on DOMContentLoaded
  document.addEventListener('DOMContentLoaded', () => {
    updateDOM();
  });
})();
