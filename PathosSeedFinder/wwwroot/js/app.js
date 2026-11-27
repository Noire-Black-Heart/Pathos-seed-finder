// Pathos Seed Finder WebGUI Application Logic
document.addEventListener('DOMContentLoaded', () => {
  // Global State
  let currentReport = null;
  let eventSource = null;
  let searchStartTime = 0;
  let activeArtifactSiteFilter = 'all';
  let activeIdentitySeriesFilter = 'all';
  let currentTileset = 'sonyvanda';
  const tileImageCache = new Map(); // "tileset:glyphName" => HTMLImageElement or null
  const baseTileSize = 32;

  const mapTilesetSelect = document.getElementById('map-tileset-select');

  function getTileUrl(name) {
    if (!name) return '';
    return `/api/tile?name=${encodeURIComponent(name)}&tileset=${encodeURIComponent(currentTileset)}`;
  }

  function updateTilesetOptionsUI() {
    document.querySelectorAll('.tileset-option').forEach(label => {
      const radio = label.querySelector('input[type="radio"]');
      if (radio && radio.checked) {
        label.classList.add('selected');
      } else {
        label.classList.remove('selected');
      }
    });
  }

  async function applyTilesetChange(newTileset, shouldPersist = false) {
    if (!newTileset) return;
    currentTileset = newTileset;

    // 1. Sync Map Toolbar Dropdown
    if (mapTilesetSelect && mapTilesetSelect.value !== newTileset) {
      mapTilesetSelect.value = newTileset;
    }

    // 2. Sync Settings Modal Radios
    const radio = document.querySelector(`input[name="settings-tileset"][value="${newTileset}"]`);
    if (radio) {
      radio.checked = true;
    }
    updateTilesetOptionsUI();

    // 3. Clear Map Texture Cache & Redraw Canvas
    tileImageCache.clear();
    if (typeof renderMapCanvas === 'function') {
      renderMapCanvas();
    }

    // 4. Update Other Views if data exists
    if (currentReport) {
      if (typeof renderArtifactsTable === 'function') renderArtifactsTable(currentReport.artifacts || []);
      if (typeof renderIdentitiesTable === 'function') renderIdentitiesTable(currentReport.identifiedItems || []);
      if (typeof renderShops === 'function') renderShops(currentReport.shops || []);
      if (typeof renderSokobanDetails === 'function') renderSokobanDetails(currentReport.sokoban);
      if (typeof renderSokobanRibbon === 'function') renderSokobanRibbon(currentReport.sokoban);
    }

    // 4.1 Update all Autocomplete & Condition Rule Tile Images on screen
    document.querySelectorAll('.autocomplete-tile-img').forEach(img => {
      const glyph = img.getAttribute('data-glyph');
      if (glyph) {
        img.src = getTileUrl(glyph);
      }
    });

    // 4.2 Refresh active preview card if present
    if (activePreviewSeed && typeof selectMatchForPreview === 'function') {
      selectMatchForPreview(activePreviewSeed);
    }

    // 5. Persist to Backend if requested
    if (shouldPersist && isConfiguredState) {
      const currentPath = settingsPathInput?.value || settingsCurrentPath?.textContent || '';
      if (currentPath && currentPath !== '未绑定') {
        try {
          await fetch('/api/settings/save', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ path: currentPath, tileset: newTileset })
          });
        } catch (err) {
          console.error('Failed to persist tileset setting:', err);
        }
      }
    }
  }

  document.querySelectorAll('.tileset-option').forEach(label => {
    label.addEventListener('click', () => {
      const radio = label.querySelector('input[type="radio"]');
      if (radio) {
        radio.checked = true;
        updateTilesetOptionsUI();
      }
    });
  });

  document.querySelectorAll('input[name="settings-tileset"]').forEach(radio => {
    radio.addEventListener('change', () => {
      updateTilesetOptionsUI();
    });
  });

  // DOM Elements - Mode Switching
  const tabAnalyzeBtn = document.getElementById('tab-analyze-btn');
  const tabFindBtn = document.getElementById('tab-find-btn');
  const viewAnalyze = document.getElementById('view-analyze');
  const viewFind = document.getElementById('view-find');

  // DOM Elements - Analyze Mode
  const analyzeSeedInput = document.getElementById('analyze-seed-input');
  const btnAnalyzeRun = document.getElementById('btn-analyze-run');
  const btnAnalyzeRandom = document.getElementById('btn-analyze-random');
  const btnClearSeed = document.getElementById('btn-clear-seed');
  const btnCopySeed = document.getElementById('btn-copy-seed');
  const btnExportMarkdown = document.getElementById('btn-export-markdown');
  const copyText = document.getElementById('copy-text');
  const copyIcon = document.getElementById('copy-icon');

  // Subtabs
  const subtabs = {
    artifacts: { btn: document.getElementById('subtab-artifacts-btn'), content: document.getElementById('subtab-artifacts-content') },
    identities: { btn: document.getElementById('subtab-identities-btn'), content: document.getElementById('subtab-identities-content') },
    map: { btn: document.getElementById('subtab-map-btn'), content: document.getElementById('subtab-map-content') },
    shops: { btn: document.getElementById('subtab-shops-btn'), content: document.getElementById('subtab-shops-content') },
    sokoban: { btn: document.getElementById('subtab-sokoban-btn'), content: document.getElementById('subtab-sokoban-content') },
  };

  // Find Mode Elements
  const btnSearchStart = document.getElementById('btn-search-start');
  const btnSearchStop = document.getElementById('btn-search-stop');
  const btnResetFilters = document.getElementById('btn-reset-filters');
  const matchesContainer = document.getElementById('matches-container');
  const matchesEmpty = document.getElementById('matches-empty');
  const searchProgressBar = document.getElementById('search-progress-bar');
  const searchStatusText = document.getElementById('search-status-text');
  const searchSpinner = document.getElementById('search-spinner');
  const matchesCountBadge = document.getElementById('matches-count-badge');
  const statTested = document.getElementById('stat-tested');
  const statMatched = document.getElementById('stat-matched');
  const statSpeed = document.getElementById('stat-speed');
  const statCurrentSeed = document.getElementById('stat-current-seed');

  // Theme Toggle
  const themeToggleBtn = document.getElementById('theme-toggle-btn');
  themeToggleBtn.addEventListener('click', () => {
    document.documentElement.classList.toggle('dark');
    themeToggleBtn.textContent = document.documentElement.classList.contains('dark') ? '🌙' : '☀️';
  });

  // -------------------------------------------------------------
  // Settings & Pathos Directory Management
  // -------------------------------------------------------------
  const settingsModal = document.getElementById('settings-modal');
  const btnOpenSettings = document.getElementById('btn-open-settings');
  const btnCloseSettings = document.getElementById('btn-close-settings');
  const btnSettingsCancel = document.getElementById('btn-settings-cancel');
  const btnSettingsSave = document.getElementById('btn-settings-save');
  const btnSettingsBrowse = document.getElementById('btn-settings-browse');
  const settingsPathInput = document.getElementById('settings-path-input');
  const settingsDot = document.getElementById('settings-dot');
  const settingsLabel = document.getElementById('settings-label');
  const settingsCurrentVersion = document.getElementById('settings-current-version');
  const settingsCurrentPath = document.getElementById('settings-current-path');
  const settingsCandidatesList = document.getElementById('settings-candidates-list');
  const settingsErrorAlert = document.getElementById('settings-error-alert');

  let isConfiguredState = false;

  // Language switcher in settings modal
  document.querySelectorAll('.language-option').forEach(label => {
    label.addEventListener('click', () => {
      const radio = label.querySelector('input[type="radio"]');
      if (radio) {
        radio.checked = true;
        I18N.setLanguage(radio.value, true);
      }
    });
  });

  // Language switcher in top nav header
  const headerLangBtn = document.getElementById('header-lang-btn');
  if (headerLangBtn) {
    headerLangBtn.addEventListener('click', () => {
      const cur = I18N.getLanguage();
      const next = cur === 'zh' ? 'en' : 'zh';
      I18N.setLanguage(next, true);
      if (isConfiguredState) {
        const currentPath = settingsPathInput?.value || settingsCurrentPath?.textContent || '';
        if (currentPath && currentPath !== '未绑定' && currentPath !== 'Not bound') {
          fetch('/api/settings/save', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ path: currentPath, tileset: currentTileset, language: next })
          }).catch(() => {});
        }
      }
    });
  }

  // Language changed listener to re-render views
  window.addEventListener('pathos:languageChanged', () => {
    if (currentReport) {
      renderReport(currentReport);
    }
    if (activePreviewSeed && typeof selectMatchForPreview === 'function') {
      selectMatchForPreview(activePreviewSeed);
    }
    if (typeof renderRulesList === 'function') {
      renderRulesList();
    }
    if (typeof renderPresetsContainer === 'function') {
      renderPresetsContainer();
    }
    if (typeof initThreadsUI === 'function') {
      initThreadsUI();
    }
  });

  function showSettingsModal() {
    settingsErrorAlert.classList.add('hidden');
    const radio = document.querySelector(`input[name="settings-tileset"][value="${currentTileset}"]`);
    if (radio) {
      radio.checked = true;
    }
    updateTilesetOptionsUI();

    const langRadio = document.querySelector(`input[name="settings-language"][value="${I18N.getLanguage()}"]`);
    if (langRadio) {
      langRadio.checked = true;
    }
    I18N.updateLanguageOptionsUI();

    settingsModal.classList.remove('hidden');
  }

  function hideSettingsModal() {
    if (!isConfiguredState) {
      alert(I18N.t('toast.needDir'));
      return;
    }
    settingsModal.classList.add('hidden');
  }

  btnOpenSettings.addEventListener('click', showSettingsModal);
  btnCloseSettings.addEventListener('click', hideSettingsModal);
  btnSettingsCancel.addEventListener('click', hideSettingsModal);

  async function loadSettings() {
    try {
      const res = await fetch('/api/settings');
      if (!res.ok) return;
      const data = await res.json();
      isConfiguredState = data.isConfigured;

      if (data.language && !localStorage.getItem('pathos_seed_finder_lang')) {
        I18N.setLanguage(data.language, false);
      }

      if (data.isConfigured) {
        settingsDot.className = 'w-2 h-2 rounded-full bg-emerald-500';
        settingsLabel.textContent = `🎮 ${data.gameVersion}`;
        settingsCurrentVersion.textContent = `${data.gameVersion} (${I18N.getLanguage() === 'en' ? 'Mounted' : '已挂载'})`;
        settingsCurrentVersion.className = 'font-bold text-emerald-400 ml-1';
        settingsCurrentPath.textContent = data.currentPath;
        settingsPathInput.value = data.currentPath;

        if (data.tileset) {
          applyTilesetChange(data.tileset, false);
        } else {
          applyTilesetChange('sonyvanda', false);
        }
      } else {
        settingsDot.className = 'w-2 h-2 rounded-full bg-red-500 animate-pulse';
        settingsLabel.textContent = I18N.t('nav.gamedir.unbound');
        settingsCurrentVersion.textContent = I18N.t('settings.unconfigured');
        settingsCurrentVersion.className = 'font-bold text-red-400 ml-1';
        settingsCurrentPath.textContent = I18N.t('settings.unbound');
        showSettingsModal();
      }

      // Render candidates
      if (data.candidates && data.candidates.length > 0) {
        settingsCandidatesList.innerHTML = data.candidates.map(c => `
          <button type="button" class="candidate-chip px-2.5 py-1 rounded-lg bg-surface-800 hover:bg-brand-500 hover:text-surface-950 text-slate-200 border border-surface-700 transition" data-path="${c}">
            📁 ${c}
          </button>
        `).join('');

        document.querySelectorAll('.candidate-chip').forEach(btn => {
          btn.addEventListener('click', () => {
            settingsPathInput.value = btn.getAttribute('data-path');
            settingsErrorAlert.classList.add('hidden');
          });
        });
      } else {
        settingsCandidatesList.innerHTML = `<span class="text-slate-500 text-xs">${I18N.t('settings.candidatesNone')}</span>`;
      }
    } catch (err) {
      console.error('Failed to load settings:', err);
    }
  }

  // Browse Folder Dialog
  btnSettingsBrowse.addEventListener('click', async () => {
    btnSettingsBrowse.disabled = true;
    btnSettingsBrowse.classList.add('opacity-75');
    try {
      const res = await fetch('/api/settings/browse', { method: 'POST' });
      if (res.ok) {
        const data = await res.json();
        if (data.success && data.selectedPath) {
          settingsPathInput.value = data.selectedPath;
          if (data.isValid) {
            settingsErrorAlert.classList.add('hidden');
          } else if (data.error) {
            settingsErrorAlert.textContent = data.error;
            settingsErrorAlert.classList.remove('hidden');
          }
        }
      }
    } catch (err) {
      console.error('Browse error:', err);
    } finally {
      btnSettingsBrowse.disabled = false;
      btnSettingsBrowse.classList.remove('opacity-75');
    }
  });

  // Save Settings
  btnSettingsSave.addEventListener('click', async () => {
    const targetPath = settingsPathInput.value.trim();
    const selectedRadio = document.querySelector('input[name="settings-tileset"]:checked');
    const selectedTileset = selectedRadio ? selectedRadio.value : 'sonyvanda';
    const selectedLangRadio = document.querySelector('input[name="settings-language"]:checked');
    const selectedLang = selectedLangRadio ? selectedLangRadio.value : I18N.getLanguage();

    if (!targetPath) {
      settingsErrorAlert.textContent = I18N.getLanguage() === 'en' ? 'Please specify a valid game root directory!' : '请输入或选择有效的游戏安装根目录！';
      settingsErrorAlert.classList.remove('hidden');
      return;
    }

    btnSettingsSave.disabled = true;
    btnSettingsSave.classList.add('opacity-75');
    settingsErrorAlert.classList.add('hidden');

    try {
      const res = await fetch('/api/settings/save', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ path: targetPath, tileset: selectedTileset, language: selectedLang })
      });
      const data = await res.json();

      if (data.success) {
        isConfiguredState = true;
        settingsModal.classList.add('hidden');
        I18N.setLanguage(selectedLang, true);
        await loadSettings();
        await loadSuggestions();
        await applyTilesetChange(data.tileset || selectedTileset, false);
        if (currentReport) {
          renderReport(currentReport);
        } else {
          runAnalyze();
        }
      } else {
        settingsErrorAlert.textContent = data.error || (I18N.getLanguage() === 'en' ? 'Validation failed. Required engine/data files not found.' : '验证失败，未找到必要的引擎与数据文件！');
        settingsErrorAlert.classList.remove('hidden');
      }
    } catch (err) {
      settingsErrorAlert.textContent = `Error: ${err.message}`;
      settingsErrorAlert.classList.remove('hidden');
    } finally {
      btnSettingsSave.disabled = false;
      btnSettingsSave.classList.remove('opacity-75');
    }
  });

  // -------------------------------------------------------------
  // Navigation Tabs Switching
  // -------------------------------------------------------------
  function switchMode(mode) {
    if (mode === 'analyze') {
      tabAnalyzeBtn.classList.add('active');
      tabAnalyzeBtn.classList.remove('text-slate-400');
      tabFindBtn.classList.remove('active');
      tabFindBtn.classList.add('text-slate-400');
      viewAnalyze.classList.remove('hidden');
      viewFind.classList.add('hidden');
    } else {
      tabFindBtn.classList.add('active');
      tabFindBtn.classList.remove('text-slate-400');
      tabAnalyzeBtn.classList.remove('active');
      tabAnalyzeBtn.classList.add('text-slate-400');
      viewFind.classList.remove('hidden');
      viewAnalyze.classList.add('hidden');
    }
  }

  tabAnalyzeBtn.addEventListener('click', () => switchMode('analyze'));
  tabFindBtn.addEventListener('click', () => switchMode('find'));

  // Subtabs Switching
  let skipNextMapReset = false;
  Object.keys(subtabs).forEach(key => {
    const tab = subtabs[key];
    if (!tab || !tab.btn || !tab.content) return;
    tab.btn.addEventListener('click', () => {
      Object.keys(subtabs).forEach(k => {
        const t = subtabs[k];
        if (t && t.btn && t.content) {
          t.btn.classList.remove('active', 'border-brand-500', 'text-brand-400', 'bg-surface-900');
          t.btn.classList.add('border-transparent', 'text-slate-400');
          t.content.classList.add('hidden');
        }
      });
      tab.btn.classList.add('active', 'border-brand-500', 'text-brand-400', 'bg-surface-900');
      tab.btn.classList.remove('border-transparent', 'text-slate-400');
      tab.content.classList.remove('hidden');

      if (key === 'map') {
        setTimeout(() => {
          resizeMapCanvas();
          if (!skipNextMapReset) {
            resetMapViewport();
          }
          skipNextMapReset = false;
        }, 50);
      }
    });
  });

  // -------------------------------------------------------------
  // Analyze Mode Logic
  // -------------------------------------------------------------
  async function runAnalyze(seed) {
    if (!seed) seed = analyzeSeedInput.value.trim() || 'test1234';
    analyzeSeedInput.value = seed;

    btnAnalyzeRun.disabled = true;
    btnAnalyzeRun.classList.add('opacity-75');

    try {
      const res = await fetch(`/api/analyze?seed=${encodeURIComponent(seed)}`);
      if (!res.ok) throw new Error(`HTTP ${res.status}: ${await res.text()}`);
      const report = await res.json();
      currentReport = report;
      renderReport(report);
    } catch (err) {
      alert(`分析失败: ${err.message}`);
    } finally {
      btnAnalyzeRun.disabled = false;
      btnAnalyzeRun.classList.remove('opacity-75');
    }
  }

  function renderSokobanRibbon(soko) {
    const sokoRewardsDiv = document.getElementById('soko-rewards');
    if (!sokoRewardsDiv) return;
    const isEn = I18N.getLanguage() === 'en';

    if (soko) {
      document.getElementById('soko-entrance').textContent = isEn
        ? `Entrance: L${soko.dungeonEntranceLevel}`
        : `入口: 第${soko.dungeonEntranceLevel}层`;
      let html = '';
      const renderPrize = (item, icon) => {
        if (!item) return '';
        const name = isEn ? item.englishName : `<b>${item.chineseName}</b> <span class="text-slate-500 font-mono">(${item.englishName})</span>`;
        return `<div class="flex items-center gap-1.5"><img src="${getTileUrl(item.glyph || item.englishName)}" class="w-4 h-4 object-contain inline-block shrink-0" onerror="this.style.display='none'"><span>${icon} ${name}</span></div>`;
      };
      if (soko.finalBag) html += renderPrize(soko.finalBag, '🎒');
      if (soko.finalCloak) html += renderPrize(soko.finalCloak, '🧥');
      if (soko.finalAmulet) html += renderPrize(soko.finalAmulet, '📿');
      sokoRewardsDiv.innerHTML = html || `<div class="text-slate-500">${I18N.t('analyze.sokobanNoRewards')}</div>`;
      document.getElementById('soko-floors-hint').textContent = isEn
        ? `Floors 1~3 magic items: ${soko.floorRewards.length}`
        : `1~3层沿途魔法道具: ${soko.floorRewards.length}件`;
    } else {
      document.getElementById('soko-entrance').textContent = isEn ? 'Entrance: --' : '入口: 未找到';
      sokoRewardsDiv.innerHTML = `<div class="text-slate-500">${I18N.t('analyze.sokobanNone')}</div>`;
    }
  }

  function getFacilityIcon(name) {
    if (!name) return '🏪';
    const n = name.toLowerCase();
    if (n.includes('兵器') || n.includes('枪') || n.includes('weapon') || n.includes('gun')) return '⚔️';
    if (n.includes('祭坛') || n.includes('altar') || n.includes('shrine')) return '⛩️';
    if (n.includes('珠宝') || n.includes('jewel')) return '💎';
    if (n.includes('盔甲') || n.includes('服装') || n.includes('armour') || n.includes('apparel')) return '🛡️';
    if (n.includes('文具') || n.includes('书') || n.includes('stationery') || n.includes('book')) return '📜';
    if (n.includes('神秘') || n.includes('据点') || n.includes('mystic') || n.includes('coven')) return '🔮';
    if (n.includes('制作') || n.includes('工坊') || n.includes('craft')) return '⚒️';
    if (n.includes('熟食') || n.includes('肉') || n.includes('delicatessen')) return '🍖';
    if (n.includes('酒') || n.includes('liquor')) return '🍷';
    if (n.includes('灯') || n.includes('五金') || n.includes('light') || n.includes('hardware')) return '🕯️';
    if (n.includes('圣殿') || n.includes('森林') || n.includes('grove') || n.includes('sepulchre')) return '✨';
    return '🏪';
  }

  function renderFacilityChips(facilities) {
    const isEn = I18N.getLanguage() === 'en';
    if (!facilities || facilities.length === 0) {
      return `<div class="text-slate-500 text-xs italic">${isEn ? 'No merchants / commercial facilities' : '无商人驻留 / 无商贸设施'}</div>`;
    }

    const counts = {};
    const order = [];
    facilities.forEach(f => {
      const name = isEn ? (f.englishType || f.chineseType || 'Facility') : (f.chineseType || f.englishType || '未知设施');
      if (!counts[name]) {
        counts[name] = 0;
        order.push(name);
      }
      counts[name]++;
    });

    const chips = order.map(name => {
      const icon = getFacilityIcon(name);
      const count = counts[name];
      const countBadge = count > 1 ? `<span class="text-amber-400 font-bold ml-0.5 text-[10px]">×${count}</span>` : '';
      return `<span class="px-2 py-0.5 rounded-lg bg-surface-950/80 text-slate-200 border border-surface-700/70 text-xs font-medium inline-flex items-center gap-1 shadow-sm hover:border-brand-500/50 transition">
        <span>${icon}</span>
        <span>${name}</span>
        ${countBadge}
      </span>`;
    }).join('');

    return `<div class="flex flex-wrap gap-1.5 max-h-[110px] overflow-y-auto pr-1">${chips}</div>`;
  }

  function renderReport(report) {
    const isEn = I18N.getLanguage() === 'en';

    // 1. Ribbon Info
    document.getElementById('info-seed-text').textContent = report.seedText;
    document.getElementById('info-seed-code').textContent = `(Code: ${report.seedCode})`;
    document.getElementById('info-elapsed-time').textContent = `${report.elapsedMs} ms`;
    document.getElementById('info-artifact-count').textContent = isEn ? `${report.totalArtifacts}` : `${report.totalArtifacts} 件`;
    document.getElementById('info-shop-count').textContent = isEn ? `${report.totalShops}` : `${report.totalShops} 处`;

    // 2. Minetown Card
    const mt = report.minetown;
    const mtBadge = document.getElementById('mt-badge');
    const mtDesc = document.getElementById('mt-desc');
    const mtFacilities = (report.shops || []).filter(s => s.site === 'Mines' && (s.subArea === '矿镇' || s.subArea === 'Minetown' || s.levelIndex === 1));

    if (mt) {
      if (mt.isOrcSiege) {
        mtBadge.className = 'px-2 py-0.5 rounded text-xs font-bold bg-red-950 text-red-400 border border-red-800';
        mtBadge.textContent = I18N.t('analyze.minetownSiege');
      } else {
        mtBadge.className = 'px-2 py-0.5 rounded text-xs font-bold bg-emerald-950 text-emerald-400 border border-emerald-800';
        mtBadge.textContent = I18N.t('analyze.minetownPeaceful');
      }

      let mtHtml = renderFacilityChips(mtFacilities);
      if (mt.hasWater || mt.hasLava) {
        mtHtml += `<div class="mt-2 pt-1.5 border-t border-surface-800/80 text-[11px] text-slate-400 flex items-center gap-2">
          <span class="text-slate-500">${I18N.t('analyze.terrain')}</span>
          ${mt.hasWater ? `<span class="text-cyan-400 font-medium">${I18N.t('analyze.terrainWater')}</span>` : ''}
          ${mt.hasLava ? `<span class="text-orange-400 font-medium">${I18N.t('analyze.terrainLava')}</span>` : ''}
        </div>`;
      }
      mtDesc.innerHTML = mtHtml;
      document.getElementById('mt-guards').textContent = mt.watchmenCount;
      document.getElementById('mt-orcs').textContent = mt.orcCount;
      document.getElementById('mt-merchants').textContent = mtFacilities.length || mt.merchantCount;
    } else {
      mtBadge.className = 'px-2 py-0.5 rounded text-xs font-bold bg-slate-800 text-slate-400';
      mtBadge.textContent = I18N.t('analyze.minetownNone');
      mtDesc.innerHTML = `<div class="text-slate-500 text-xs italic">${I18N.t('analyze.minetownNoneDesc')}</div>`;
    }

    // 3. Elf Kingdom Card
    const elf = report.elfKingdom;
    const elfBadge = document.getElementById('elf-badge');
    const elfDesc = document.getElementById('elf-desc');
    const elfFacilities = (report.shops || []).filter(s => s.site === 'Elf Kingdom' || (s.site && s.site.includes('Elf')) || (s.siteChinese && s.siteChinese.includes('精灵')));

    if (elf) {
      if (elf.isMassacre) {
        elfBadge.className = 'px-2 py-0.5 rounded text-xs font-bold bg-red-950 text-red-400 border border-red-800';
        elfBadge.textContent = I18N.t('analyze.elfRebellion');
      } else {
        elfBadge.className = 'px-2 py-0.5 rounded text-xs font-bold bg-emerald-950 text-emerald-400 border border-emerald-800';
        elfBadge.textContent = I18N.t('analyze.elfPeaceful');
      }

      let elfHtml = renderFacilityChips(elfFacilities);
      if (elf.kingState || elf.artifactState) {
        elfHtml += `<div class="mt-2 pt-1.5 border-t border-surface-800/80 text-[11px] text-slate-400 flex flex-wrap items-center gap-x-2 gap-y-0.5">
          ${elf.kingState ? `<span>👑 ${elf.kingState}</span>` : ''}
          ${elf.artifactState ? `<span>✨ ${elf.artifactState}</span>` : ''}
        </div>`;
      }
      elfDesc.innerHTML = elfHtml;
      document.getElementById('elf-town').textContent = elf.townHighElfCount;
      document.getElementById('elf-palace').textContent = elf.palaceHighElfCount;
      document.getElementById('elf-level').textContent = isEn ? `Dungeon Level ${elf.dungeonEntranceLevel}` : `地牢 第${elf.dungeonEntranceLevel}层`;
    } else {
      elfBadge.className = 'px-2 py-0.5 rounded text-xs font-bold bg-slate-800 text-slate-400';
      elfBadge.textContent = I18N.t('analyze.elfNone');
      elfDesc.innerHTML = `<div class="text-slate-500 text-xs italic">${I18N.t('analyze.elfNoneDesc')}</div>`;
    }

    // 4. Sokoban Card
    renderSokobanRibbon(report.sokoban);

    // 5. Subtab Artifacts
    document.getElementById('tab-art-count').textContent = report.totalArtifacts;
    document.getElementById('tab-shop-count').textContent = report.totalShops;
    document.getElementById('tab-id-count').textContent = report.totalIdentifiedItems || (report.identifiedItems ? report.identifiedItems.length : 0);
    renderArtifactsTable(report.artifacts);

    // 5.5. Subtab Identities (All Potion & Scroll Identification)
    renderIdentitiesTable(report.identifiedItems || []);

    // 5.6. Map Catalog & Floor Loading
    loadMapCatalog(report.seedText);

    // 6. Subtab Shops
    renderShops(report.shops);

    // 7. Subtab Sokoban
    renderSokobanDetails(report.sokoban);
  }

  function renderArtifactsTable(artifacts) {
    const tbody = document.getElementById('artifacts-table-body');
    const query = (document.getElementById('artifact-filter-input').value || '').trim().toLowerCase();
    const isEn = I18N.getLanguage() === 'en';

    const filtered = (artifacts || []).filter(art => {
      // Site filter
      if (activeArtifactSiteFilter !== 'all') {
        if (!art.site.toLowerCase().includes(activeArtifactSiteFilter.toLowerCase()) && 
            !art.siteChinese.toLowerCase().includes(activeArtifactSiteFilter.toLowerCase())) {
          return false;
        }
      }
      // Text query
      if (!query) return true;
      return art.englishName.toLowerCase().includes(query) ||
             art.chineseName.toLowerCase().includes(query) ||
             art.site.toLowerCase().includes(query) ||
             art.siteChinese.toLowerCase().includes(query) ||
             art.holderName.toLowerCase().includes(query) ||
             art.holderChinese.toLowerCase().includes(query);
    });

    if (filtered.length === 0) {
      tbody.innerHTML = `<tr><td colspan="6" class="py-8 text-center text-slate-500">${I18N.t('art.empty')}</td></tr>`;
      return;
    }

    tbody.innerHTML = filtered.map(art => {
      const possessionBadge = I18N.formatPossessionBadge(art.possession, art.isLocked, art.isTrapped);
      const holder = I18N.formatHolder(art.holderName, art.holderChinese);
      const location = I18N.formatLocation(art.site, art.siteChinese, art.levelIndex, art.dungeonLevel, art.subArea);
      const locBtn = `
        <button type="button" 
                class="jump-to-artifact-map-btn group/btn px-2.5 py-1 rounded-lg bg-surface-950/80 hover:bg-brand-500/20 text-slate-200 hover:text-brand-300 border border-surface-700/80 hover:border-brand-500/60 transition inline-flex items-center gap-1.5 text-xs font-medium cursor-pointer active:scale-95 text-left"
                data-raw-name="${art.mapRawName || ''}"
                data-site="${art.site || ''}"
                data-level="${art.levelIndex || 0}"
                data-gx="${art.x}"
                data-gy="${art.y}"
                data-name="${isEn ? art.englishName : (art.chineseName || art.englishName)}"
                title="${isEn ? 'Click to inspect in Map Visualizer' : '点击立刻跳转到全层地图透视并聚焦该神器'}">
          <span class="font-semibold">${location}</span>
          <span class="text-[10px] text-slate-500 group-hover/btn:text-brand-400">➜</span>
        </button>
      `;

      const nameHtml = isEn
        ? `<div class="font-bold text-amber-400 font-mono">${art.englishName}</div>`
        : `<div class="font-bold text-amber-400 font-sans">${art.chineseName}</div>
           <div class="text-xs text-slate-500 font-mono">${art.englishName}</div>`;

      return `
        <tr class="hover:bg-surface-850/60 transition">
          <td class="py-2.5 px-3">
            <div class="flex items-center space-x-2.5">
              <img src="${getTileUrl(art.glyph || art.englishName)}" class="w-8 h-8 rounded bg-surface-950/70 p-0.5 border border-surface-700/60 object-contain shrink-0 shadow-sm" onerror="this.style.display='none'" alt="${art.englishName}">
              <div>
                ${nameHtml}
              </div>
            </div>
          </td>
          <td class="py-2.5 px-3 font-sans text-slate-200">
            ${locBtn}
          </td>
          <td class="py-2.5 px-3 font-mono text-amber-300">
            ${I18N.t('art.dungeonLevelFormat', art.dungeonLevel)}
          </td>
          <td class="py-2.5 px-3">
            ${possessionBadge}
          </td>
          <td class="py-2.5 px-3 text-slate-300 font-sans">
            ${holder}
          </td>
          <td class="py-2.5 px-3 text-right font-mono text-slate-400">
            (${art.x}, ${art.y})
          </td>
        </tr>
      `;
    }).join('');
  }

  function escapeHtml(str) {
    return String(str ?? '')
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;');
  }

  let activeShopTypeFilter = 'all';
  let shopDungeonOnly = false;

  function isMainDungeonSite(site) {
    if (!site) return false;
    const s = site.toLowerCase();
    return s === 'dungeon' || s === '主地牢' || s === '地牢';
  }

  function compareShopEarliest(a, b) {
    const da = (a.dungeonLevel && a.dungeonLevel > 0) ? a.dungeonLevel : (a.levelIndex || 999);
    const db = (b.dungeonLevel && b.dungeonLevel > 0) ? b.dungeonLevel : (b.levelIndex || 999);
    if (da !== db) return da - db;
    // Prefer Main Dungeon over branch when equivalent depth ties
    const aMain = isMainDungeonSite(a.site) ? 0 : 1;
    const bMain = isMainDungeonSite(b.site) ? 0 : 1;
    if (aMain !== bMain) return aMain - bMain;
    const la = a.levelIndex || 0;
    const lb = b.levelIndex || 0;
    if (la !== lb) return la - lb;
    return (a.englishType || '').localeCompare(b.englishType || '');
  }

  function formatShopShortDepth(s, isEn) {
    const isMain = isMainDungeonSite(s.site);
    const dLvl = (s.dungeonLevel && s.dungeonLevel > 0) ? s.dungeonLevel : (s.levelIndex || 1);
    if (isMain) {
      return isEn ? `L${dLvl}` : `主地牢 ${dLvl}层`;
    }
    const siteLabel = isEn
      ? (s.site || s.siteChinese || '')
      : (s.subArea || s.siteChinese || s.site || '');
    return isEn
      ? `${siteLabel} (≈L${dLvl})`
      : `${siteLabel} (折算${dLvl}层)`;
  }

  function renderShops(shops) {
    const grid = document.getElementById('shops-grid');
    const summaryEl = document.getElementById('shops-earliest-summary');
    const filteredHint = document.getElementById('shops-filtered-hint');
    const resetBtn = document.getElementById('shop-filter-reset');
    const searchInput = document.getElementById('shop-filter-input');
    const isEn = I18N.getLanguage() === 'en';
    const allShops = shops || [];

    document.getElementById('shops-count-badge').textContent = I18N.t('shops.countBadge', allShops.length);

    if (allShops.length === 0) {
      if (summaryEl) summaryEl.innerHTML = `<span class="text-xs text-slate-500">${I18N.t('shops.empty')}</span>`;
      if (filteredHint) filteredHint.textContent = '';
      grid.innerHTML = `<div class="text-slate-500 text-center col-span-3 py-6">${I18N.t('shops.empty')}</div>`;
      return;
    }

    const scopeShops = shopDungeonOnly
      ? allShops.filter(s => isMainDungeonSite(s.site))
      : allShops;

    // 1. Build earliest appearance map per shop/facility type
    const byType = new Map();
    for (const s of scopeShops) {
      const key = (s.englishType || s.chineseType || 'unknown').toLowerCase();
      if (!byType.has(key)) {
        byType.set(key, []);
      }
      byType.get(key).push(s);
    }

    const earliestEntries = [];
    byType.forEach((list, key) => {
      list.sort(compareShopEarliest);
      earliestEntries.push({
        key,
        earliest: list[0],
        count: list.length
      });
    });
    earliestEntries.sort((a, b) => compareShopEarliest(a.earliest, b.earliest));

    // If previously selected type no longer exists in current scope, reset to 'all'
    if (activeShopTypeFilter !== 'all' && !byType.has(activeShopTypeFilter)) {
      activeShopTypeFilter = 'all';
    }

    if (summaryEl) {
      if (earliestEntries.length === 0) {
        summaryEl.innerHTML = `<span class="text-xs text-slate-500">${I18N.t('shops.empty')}</span>`;
      } else {
        summaryEl.innerHTML = earliestEntries.map(({ key, earliest, count }) => {
          const isSelected = activeShopTypeFilter === key;
          const label = isEn ? (earliest.englishType || earliest.chineseType) : (earliest.chineseType || earliest.englishType);
          const depthText = formatShopShortDepth(earliest, isEn);
          const dLvl = (earliest.dungeonLevel && earliest.dungeonLevel > 0) ? earliest.dungeonLevel : (earliest.levelIndex || 99);
          const depthColor = dLvl <= 5
            ? 'text-emerald-300'
            : dLvl <= 10
            ? 'text-amber-300'
            : 'text-slate-400';

          return `
            <div class="inline-flex items-center rounded-lg border transition text-xs overflow-hidden ${isSelected ? 'bg-brand-500/20 border-brand-400 shadow-sm shadow-brand-500/20' : 'bg-surface-900/90 border-surface-700/80 hover:border-amber-500/50'}">
              <button type="button" class="shop-earliest-chip px-2.5 py-1.5 flex items-center gap-1.5 text-left" data-shop-key="${escapeHtml(key)}">
                <span class="font-semibold ${isSelected ? 'text-brand-200' : 'text-slate-200'}">${escapeHtml(label)}</span>
                <span class="font-mono font-bold ${depthColor}">${escapeHtml(depthText)}</span>
                <span class="text-[10px] text-slate-500 font-mono">×${count}</span>
              </button>
              <button type="button" class="shop-jump-btn px-1.5 py-1.5 border-l border-surface-800 hover:bg-brand-500/20 text-slate-400 hover:text-amber-300 transition"
                title="${isEn ? 'Jump to earliest on map' : '跳转到最早出现位置的地图'}"
                data-raw="${escapeHtml(earliest.mapRawName || '')}"
                data-site="${escapeHtml(earliest.site || '')}"
                data-lvl="${earliest.levelIndex || 1}"
                data-x="${earliest.x || 0}"
                data-y="${earliest.y || 0}"
                data-name="${escapeHtml(label)}">📍</button>
            </div>
          `;
        }).join('');

        summaryEl.querySelectorAll('.shop-earliest-chip').forEach(btn => {
          btn.addEventListener('click', () => {
            const key = btn.getAttribute('data-shop-key');
            activeShopTypeFilter = (activeShopTypeFilter === key) ? 'all' : key;
            renderShops(currentReport ? currentReport.shops : allShops);
          });
        });

        summaryEl.querySelectorAll('.shop-jump-btn').forEach(btn => {
          btn.addEventListener('click', (e) => {
            e.stopPropagation();
            jumpToArtifactMap(
              btn.getAttribute('data-raw') || '',
              btn.getAttribute('data-site') || '',
              parseInt(btn.getAttribute('data-lvl') || '1', 10),
              parseInt(btn.getAttribute('data-x') || '0', 10),
              parseInt(btn.getAttribute('data-y') || '0', 10),
              btn.getAttribute('data-name') || ''
            );
          });
        });
      }
    }

    // 2. Apply type + text filter and sort earliest-first
    const query = (searchInput?.value || '').trim().toLowerCase();
    const filtered = scopeShops.filter(s => {
      if (activeShopTypeFilter !== 'all') {
        const key = (s.englishType || s.chineseType || 'unknown').toLowerCase();
        if (key !== activeShopTypeFilter) return false;
      }
      if (query) {
        const hay = `${s.chineseType || ''} ${s.englishType || ''} ${s.merchantName || ''} ${s.site || ''} ${s.siteChinese || ''} ${s.subArea || ''}`.toLowerCase();
        if (!hay.includes(query)) return false;
      }
      return true;
    }).sort(compareShopEarliest);

    const hasActiveFilter = activeShopTypeFilter !== 'all' || !!query || shopDungeonOnly;
    if (resetBtn) {
      resetBtn.classList.toggle('hidden', !hasActiveFilter);
    }
    if (filteredHint) {
      filteredHint.textContent = I18N.t('shops.filteredHint', filtered.length, allShops.length);
    }

    if (filtered.length === 0) {
      grid.innerHTML = `<div class="text-slate-500 text-center col-span-3 py-6">${I18N.t('shops.empty')}</div>`;
      return;
    }

    // Track earliest instance of each type within the current scope so we can badge it
    const earliestOfTypeSet = new Set(earliestEntries.map(e => e.earliest));

    grid.innerHTML = filtered.map((s, idx) => {
      const dLvl = (s.dungeonLevel && s.dungeonLevel > 0) ? s.dungeonLevel : (s.levelIndex || 99);
      const isEarly = dLvl <= 10;
      const isTypeEarliest = earliestOfTypeSet.has(s) && (activeShopTypeFilter !== 'all' || !!query || idx === 0);
      const typePrimary = isEn ? (s.englishType || s.chineseType) : (s.chineseType || s.englishType);
      const typeSecondary = isEn ? (s.chineseType !== s.englishType ? s.chineseType : '') : s.englishType;
      const locStr = I18N.formatLocation(s.site, s.siteChinese, s.levelIndex, s.dungeonLevel, s.subArea);
      const keeperName = s.merchantName || I18N.t('shops.noKeeper');

      return `
        <div class="bg-surface-950/80 border ${isTypeEarliest ? 'border-emerald-500/70 shadow-md shadow-emerald-950/40' : isEarly ? 'border-amber-700/50' : 'border-surface-800'} rounded-xl p-3 flex flex-col justify-between hover:border-brand-500 transition">
          <div>
            <div class="flex items-start justify-between gap-2 mb-1.5">
              <div class="flex items-center flex-wrap gap-1.5 min-w-0">
                <span class="font-bold text-slate-200 font-sans text-sm">${escapeHtml(typePrimary)}</span>
                ${isTypeEarliest ? `<span class="px-1.5 py-0.5 rounded text-[10px] font-bold bg-emerald-950/90 text-emerald-300 border border-emerald-700/80 whitespace-nowrap">${I18N.t('shops.earliestBadge')}</span>` : ''}
              </div>
              <span class="text-xs font-mono px-1.5 py-0.5 rounded shrink-0 ${isEarly ? 'bg-amber-950 text-amber-300 border border-amber-800' : 'bg-surface-800 text-slate-400'}">
                ${escapeHtml(locStr)}
              </span>
            </div>
            ${typeSecondary ? `<div class="text-xs text-slate-500 font-mono mb-2">${escapeHtml(typeSecondary)}</div>` : ''}
          </div>
          <div class="text-xs pt-2 border-t border-surface-800 flex items-center justify-between gap-2 text-slate-400">
            <span class="truncate">${I18N.t('shops.keeper')} <b class="text-slate-300">${escapeHtml(keeperName)}</b></span>
            <div class="flex items-center gap-2 shrink-0">
              <span class="font-mono text-slate-500">(${s.x}, ${s.y})</span>
              <button type="button" class="shop-card-jump px-2 py-0.5 rounded-md bg-surface-800 hover:bg-brand-500/20 text-slate-300 hover:text-amber-300 border border-surface-700/80 text-[11px] transition"
                data-raw="${escapeHtml(s.mapRawName || '')}"
                data-site="${escapeHtml(s.site || '')}"
                data-lvl="${s.levelIndex || 1}"
                data-x="${s.x || 0}"
                data-y="${s.y || 0}"
                data-name="${escapeHtml(typePrimary)}">${I18N.t('shops.jumpMap')}</button>
            </div>
          </div>
        </div>
      `;
    }).join('');

    grid.querySelectorAll('.shop-card-jump').forEach(btn => {
      btn.addEventListener('click', () => {
        jumpToArtifactMap(
          btn.getAttribute('data-raw') || '',
          btn.getAttribute('data-site') || '',
          parseInt(btn.getAttribute('data-lvl') || '1', 10),
          parseInt(btn.getAttribute('data-x') || '0', 10),
          parseInt(btn.getAttribute('data-y') || '0', 10),
          btn.getAttribute('data-name') || ''
        );
      });
    });
  }

  // Shop Filter Controls Events
  const shopFilterInput = document.getElementById('shop-filter-input');
  if (shopFilterInput) {
    shopFilterInput.addEventListener('input', () => {
      if (currentReport) renderShops(currentReport.shops || []);
    });
  }

  const shopDungeonOnlyToggle = document.getElementById('shop-dungeon-only-toggle');
  if (shopDungeonOnlyToggle) {
    shopDungeonOnlyToggle.addEventListener('click', () => {
      shopDungeonOnly = !shopDungeonOnly;
      shopDungeonOnlyToggle.classList.toggle('bg-brand-500', shopDungeonOnly);
      shopDungeonOnlyToggle.classList.toggle('text-surface-950', shopDungeonOnly);
      shopDungeonOnlyToggle.classList.toggle('font-bold', shopDungeonOnly);
      shopDungeonOnlyToggle.classList.toggle('bg-surface-800', !shopDungeonOnly);
      shopDungeonOnlyToggle.classList.toggle('text-slate-300', !shopDungeonOnly);
      if (currentReport) renderShops(currentReport.shops || []);
    });
  }

  const shopFilterReset = document.getElementById('shop-filter-reset');
  if (shopFilterReset) {
    shopFilterReset.addEventListener('click', () => {
      activeShopTypeFilter = 'all';
      shopDungeonOnly = false;
      if (shopFilterInput) shopFilterInput.value = '';
      if (shopDungeonOnlyToggle) {
        shopDungeonOnlyToggle.classList.remove('bg-brand-500', 'text-surface-950', 'font-bold');
        shopDungeonOnlyToggle.classList.add('bg-surface-800', 'text-slate-300');
      }
      if (currentReport) renderShops(currentReport.shops || []);
    });
  }

  function renderSokobanDetails(soko) {
    const container = document.getElementById('sokoban-details-view');
    const isEn = I18N.getLanguage() === 'en';
    if (!soko) {
      container.innerHTML = `<div class="text-slate-500 text-center py-6">${I18N.t('soko.empty')}</div>`;
      return;
    }

    const renderItemCard = (label, item) => {
      const name = isEn 
        ? `<span class="font-bold text-slate-200 text-sm font-mono">${item?.englishName || I18N.t('soko.none')}</span>`
        : `<span class="font-bold text-slate-200 text-sm">${item?.chineseName || '无'}</span><span class="text-slate-500 block font-mono text-xs">${item?.englishName || ''}</span>`;
      return `
        <div class="bg-surface-900 p-3 rounded-lg border border-surface-800 flex items-center space-x-3">
          <img src="${getTileUrl(item?.glyph || item?.englishName)}" class="w-8 h-8 rounded bg-surface-950 p-0.5 border border-surface-700 object-contain shrink-0" onerror="this.style.display='none'">
          <div>
            <span class="text-slate-500 block mb-0.5">${label}</span>
            ${name}
          </div>
        </div>
      `;
    };

    let html = `
      <div class="bg-surface-950 p-4 rounded-xl border border-surface-800">
        <h4 class="font-bold text-amber-400 text-sm mb-3">${I18N.t('soko.big3Title')}</h4>
        <div class="grid grid-cols-1 md:grid-cols-3 gap-3 text-xs">
          ${renderItemCard(I18N.t('soko.bag'), soko.finalBag)}
          ${renderItemCard(I18N.t('soko.cloak'), soko.finalCloak)}
          ${renderItemCard(I18N.t('soko.amulet'), soko.finalAmulet)}
        </div>
      </div>

      <div class="bg-surface-950 p-4 rounded-xl border border-surface-800">
        <h4 class="font-bold text-slate-300 text-sm mb-3">${I18N.t('soko.rewardsTitle', soko.floorRewards.length)}</h4>
        <div class="grid grid-cols-1 md:grid-cols-2 gap-2 text-xs">
          ${soko.floorRewards.map(r => {
            const rewardName = isEn
              ? `<span class="font-bold text-slate-200 font-mono">${r.englishName}</span>`
              : `<span class="font-bold text-slate-200 font-sans">${r.chineseName}</span><span class="text-slate-500 font-mono text-xs ml-1">(${r.englishName})</span>`;
            return `
              <div class="bg-surface-900 p-2.5 rounded-lg border border-surface-800 flex items-center justify-between">
                <div>
                  ${rewardName}
                </div>
                <span class="px-2 py-0.5 rounded bg-surface-800 text-amber-300 font-mono">${I18N.t('soko.floorFormat', r.floor)}</span>
              </div>
            `;
          }).join('')}
        </div>
      </div>
    `;

    container.innerHTML = html;
  }

  function renderBranchesTable(branches) {
    const tbody = document.getElementById('branches-table-body');
    if (!tbody) return;
    const isEn = I18N.getLanguage() === 'en';
    if (!branches || branches.length === 0) {
      tbody.innerHTML = `<tr><td colspan="5" class="py-8 text-center text-slate-500">${isEn ? 'No branch entrances detected' : '未检测到地牢分支入口'}</td></tr>`;
      return;
    }

    tbody.innerHTML = branches.map(b => `
      <tr class="hover:bg-surface-850/60 transition">
        <td class="py-2.5 px-3 font-mono font-bold text-amber-400">
          ${I18N.t('art.dungeonLevelFormat', b.dungeonLevel)}
        </td>
        <td class="py-2.5 px-3 font-sans text-slate-200 font-semibold">
          ${b.displayBranch}
        </td>
        <td class="py-2.5 px-3 font-sans text-slate-300">
          ${isEn ? (b.portalName || b.portalChinese) : `${b.portalChinese} <span class="text-slate-500">(${b.portalName})</span>`}
        </td>
        <td class="py-2.5 px-3 text-slate-400 font-mono text-xs">
          ${b.destinationMap}
        </td>
        <td class="py-2.5 px-3 text-right font-mono text-slate-400">
          (${b.x}, ${b.y})
        </td>
      </tr>
    `).join('');
  }

  function renderIdentitiesTable(items) {
    const tbody = document.getElementById('identities-table-body');
    const hint = document.getElementById('identities-count-hint');
    const query = (document.getElementById('identity-filter-input').value || '').trim().toLowerCase();
    const isEn = I18N.getLanguage() === 'en';

    const filtered = (items || []).filter(item => {
      // Category filter
      if (activeIdentitySeriesFilter !== 'all') {
        if (activeIdentitySeriesFilter === 'gear') {
          if (['potion', 'scroll', 'wand', 'ring', 'amulet', 'book'].includes(item.series)) {
            return false;
          }
        } else if (item.series !== activeIdentitySeriesFilter) {
          return false;
        }
      }

      // Query filter
      if (!query) return true;
      return (item.trueNameCn && item.trueNameCn.toLowerCase().includes(query)) ||
             (item.trueNameEn && item.trueNameEn.toLowerCase().includes(query)) ||
             (item.apparentNameCn && item.apparentNameCn.toLowerCase().includes(query)) ||
             (item.apparentNameEn && item.apparentNameEn.toLowerCase().includes(query)) ||
             (item.seriesChinese && item.seriesChinese.toLowerCase().includes(query)) ||
             (item.series && item.series.toLowerCase().includes(query));
    });

    if (hint) {
      hint.textContent = I18N.t('id.countHint', items ? items.length : 0, filtered.length);
    }

    if (filtered.length === 0) {
      tbody.innerHTML = `<tr><td colspan="5" class="py-8 text-center text-slate-500">${I18N.t('id.empty')}</td></tr>`;
      return;
    }

    tbody.innerHTML = filtered.map(item => {
      let badgeClass = 'badge-series-gear';
      let icon = '🛡️';
      if (item.series === 'potion') { badgeClass = 'badge-series-potion'; icon = '🧪'; }
      else if (item.series === 'scroll') { badgeClass = 'badge-series-scroll'; icon = '📜'; }
      else if (item.series === 'wand') { badgeClass = 'badge-series-wand'; icon = '🪄'; }
      else if (item.series === 'ring') { badgeClass = 'badge-series-ring'; icon = '💍'; }
      else if (item.series === 'amulet') { badgeClass = 'badge-series-amulet'; icon = '📿'; }
      else if (item.series === 'book') { badgeClass = 'badge-series-book'; icon = '📖'; }

      const seriesName = I18N.formatSeriesName(item);

      const apparentHtml = isEn
        ? `<div class="font-bold text-slate-100 font-mono text-sm">${item.apparentNameEn}</div>`
        : `<div class="font-bold text-slate-100 font-sans text-sm">${item.apparentNameCn}</div>
           <div class="text-[11px] text-slate-400 font-mono">${item.apparentNameEn}</div>`;

      const trueHtml = isEn
        ? `<div class="font-bold text-amber-300 font-mono text-sm">${item.trueNameEn}</div>`
        : `<div class="font-bold text-amber-300 font-sans text-sm">${item.trueNameCn}</div>
           <div class="text-[11px] text-slate-400 font-mono">${item.trueNameEn}</div>`;

      return `
        <tr class="hover:bg-surface-850/60 transition">
          <td class="py-2.5 px-3">
            <span class="px-2 py-0.5 rounded text-xs font-semibold ${badgeClass}">
              ${icon} ${seriesName}
            </span>
          </td>
          <td class="py-2.5 px-3">
            <div class="flex items-center space-x-2.5">
              <img src="${getTileUrl(item.glyph || item.apparentNameEn)}" class="w-8 h-8 rounded bg-surface-950/70 p-0.5 border border-surface-700/60 object-contain shrink-0 shadow-sm" onerror="this.style.display='none'" alt="${item.apparentNameEn}">
              <div>
                ${apparentHtml}
              </div>
            </div>
          </td>
          <td class="py-2.5 px-3 text-center text-amber-500 font-bold select-none">
            ➜
          </td>
          <td class="py-2.5 px-3">
            <div class="flex items-center space-x-2.5">
              <img src="${getTileUrl(item.glyph || item.apparentNameEn)}" class="w-6 h-6 rounded bg-surface-950/50 p-0.5 border border-surface-800 object-contain shrink-0" onerror="this.style.display='none'" alt="${item.trueNameEn}">
              <div>
                ${trueHtml}
              </div>
            </div>
          </td>
          <td class="py-2.5 px-3 text-right">
            <code class="text-slate-400 text-xs font-mono bg-surface-950 px-2 py-1 rounded border border-surface-800 select-all">${item.trueNameEn}</code>
          </td>
        </tr>
      `;
    }).join('');
  }

  // Identity Filter Input Events
  const identityFilterInput = document.getElementById('identity-filter-input');
  identityFilterInput.addEventListener('input', () => {
    if (currentReport) renderIdentitiesTable(currentReport.identifiedItems || []);
  });

  // Identity Series Filter Chips
  document.querySelectorAll('.id-series-chip').forEach(chip => {
    chip.addEventListener('click', () => {
      document.querySelectorAll('.id-series-chip').forEach(c => {
        c.classList.remove('active', 'bg-brand-500', 'text-surface-950', 'font-bold');
        c.classList.add('bg-surface-800', 'text-slate-300', 'font-medium');
      });
      chip.classList.add('active', 'bg-brand-500', 'text-surface-950', 'font-bold');
      chip.classList.remove('bg-surface-800', 'text-slate-300', 'font-medium');
      activeIdentitySeriesFilter = chip.getAttribute('data-series');
      if (currentReport) renderIdentitiesTable(currentReport.identifiedItems || []);
    });
  });

  // Artifact Filter Input Events
  document.getElementById('artifact-filter-input').addEventListener('input', () => {
    if (currentReport) renderArtifactsTable(currentReport.artifacts);
  });

  const artifactsTableBody = document.getElementById('artifacts-table-body');
  if (artifactsTableBody) {
    artifactsTableBody.addEventListener('click', (e) => {
      const btn = e.target.closest('.jump-to-artifact-map-btn');
      if (!btn) return;
      const rawName = btn.getAttribute('data-raw-name');
      const site = btn.getAttribute('data-site');
      const level = btn.getAttribute('data-level');
      const gx = parseInt(btn.getAttribute('data-gx'), 10);
      const gy = parseInt(btn.getAttribute('data-gy'), 10);
      const name = btn.getAttribute('data-name');
      jumpToArtifactMap(rawName, site, level, gx, gy, name);
    });
  }

  // Site Filter Chips
  document.querySelectorAll('.site-chip').forEach(chip => {
    chip.addEventListener('click', () => {
      document.querySelectorAll('.site-chip').forEach(c => {
        c.classList.remove('active', 'bg-brand-500', 'text-surface-950', 'font-bold');
        c.classList.add('bg-surface-800', 'text-slate-300', 'font-medium');
      });
      chip.classList.add('active', 'bg-brand-500', 'text-surface-950', 'font-bold');
      chip.classList.remove('bg-surface-800', 'text-slate-300', 'font-medium');
      activeArtifactSiteFilter = chip.getAttribute('data-site');
      if (currentReport) renderArtifactsTable(currentReport.artifacts);
    });
  });

  // Run Analyze Button
  btnAnalyzeRun.addEventListener('click', () => runAnalyze());
  analyzeSeedInput.addEventListener('keydown', (e) => {
    if (e.key === 'Enter') runAnalyze();
  });

  // Random Seed Button
  btnAnalyzeRandom.addEventListener('click', () => {
    const randomHex = '#' + Math.floor(Math.random() * 0x7FFFFFFF).toString(16).toUpperCase().padStart(8, '0');
    runAnalyze(randomHex);
  });

  // Clear Seed Button
  btnClearSeed.addEventListener('click', () => {
    analyzeSeedInput.value = '';
    analyzeSeedInput.focus();
  });

  // Copy Seed Button
  btnCopySeed.addEventListener('click', async () => {
    if (!currentReport) return;
    try {
      await navigator.clipboard.writeText(currentReport.seedText);
      copyText.textContent = I18N.t('analyze.btnCopied');
      copyIcon.textContent = '✓';
      setTimeout(() => {
        copyText.textContent = I18N.t('analyze.btnCopy');
        copyIcon.textContent = '📋';
      }, 2000);
    } catch {
      alert(I18N.isZh() ? '剪贴板复制失败，请手动选择复制' : 'Failed to copy to clipboard, please copy manually');
    }
  });

  // Export Markdown Button
  btnExportMarkdown.addEventListener('click', () => {
    if (!currentReport) return;
    window.open(`/api/export-markdown?seed=${encodeURIComponent(currentReport.seedText)}`, '_blank');
  });

  // -------------------------------------------------------------
  // Filter / Roll Mode 2.0 (Dynamic Multi-Condition & In-Place Preview)
  // -------------------------------------------------------------
  // Cached suggestions database (Offline fallback + async API fetch)
  let suggestionsData = {
    artifacts: [
      { cn: '明镜', en: 'Mirrorbright' },
      { cn: '三千惊雷枪', en: 'Drilanze' },
      { cn: '太阳神之眼', en: 'Eyes of Ra' },
      { cn: '埃塞俄比加之眼', en: 'Eye of Aethiopica' },
      { cn: '佛盘剑', en: 'Vorpal Blade' },
      { cn: '邪兽之牙', en: 'Grimtooth' },
      { cn: '逃脱专家', en: 'Escapist' },
      { cn: '巨型挖掘者', en: 'Colossal Excavator' },
      { cn: '“英雄”', en: 'The Hero' },
      { cn: '宵暗', en: 'Blinderäg' },
      { cn: '双子星', en: 'Harbalest' },
      { cn: '鸦羽弓', en: 'Ravenbow' },
      { cn: '有印章的信', en: 'Stamped Letter' },
      { cn: '光明', en: 'Illuminare' },
      { cn: '裂地锋芒', en: 'Chasm Edge' },
      { cn: '魅血', en: 'Rosenthral' },
      { cn: '撕裂者', en: 'Cleaver' },
      { cn: '神圣复仇者', en: 'Holy Avenger' },
      { cn: '斯汀', en: 'Sting' },
      { cn: '风暴使者', en: 'Stormbringer' },
      { cn: '石中剑', en: 'Excalibur' },
      { cn: '神圣宙斯之盾', en: 'Aegis' },
      { cn: '无尽之水袋', en: 'Everfull Purse' },
      { cn: '魔能转换器', en: 'Magic Converter' },
      { cn: '古武士刀', en: 'Ancient Katana' },
      { cn: '仙女座', en: 'Aurigage' },
      { cn: '女巫扫帚', en: 'Brumstyk' },
      { cn: '胄甲', en: 'Carapace' },
      { cn: '混乱之锤', en: 'Chaoshammer' },
      { cn: '幼芽', en: 'Creeping Sprout' },
      { cn: '枯枝', en: 'Deadwood' },
      { cn: '沙漠之鹰', en: 'Deagle' },
      { cn: '血色蔷薇', en: 'Dire Needle' },
      { cn: '斩龙剑', en: 'Dragonbane' },
      { cn: '巨人杀手', en: 'Giantslayer' },
      { cn: '抽动之舌', en: 'Lashing Tongue' },
      { cn: '裁决者', en: 'Magistrator' },
      { cn: '正宗', en: 'Masamune' },
      { cn: '村正', en: 'Muramasa' },
      { cn: '冰霜旋涡', en: 'Maulfrost' },
      { cn: '哲学者之石', en: 'Philosophers Stone' },
      { cn: '智慧之镜', en: 'Prudentia' },
      { cn: '符文剑', en: 'Runesword' },
      { cn: '阳炎剑', en: 'Sunsword' },
      { cn: '泰拉瑞亚', en: 'Talaria' },
      { cn: '威利姆之壁', en: 'Verimurus' },
      { cn: '雷霆长矛', en: 'Voltaic' },
      { cn: '凋零', en: 'Witherloch' }
    ],
    sokobanPrizes: [
      { cn: '收纳之袋', en: 'bag of holding' },
      { cn: '诡计之袋', en: 'bag of tricks' },
      { cn: '反射护符', en: 'amulet of reflection' },
      { cn: '吸收抗性护符', en: 'amulet of drain resistance' },
      { cn: '生命护符', en: 'amulet of life saving' },
      { cn: '续命护符', en: 'amulet of life saving' },
      { cn: '隐形斗篷', en: 'cloak of invisibility' },
      { cn: '位移斗篷', en: 'cloak of displacement' },
      { cn: '油布斗篷', en: 'oilskin cloak' },
      { cn: '防护斗篷', en: 'cloak of protection' },
      { cn: '隐身之戒', en: 'ring of invisibility' },
      { cn: '自由行动之戒', en: 'ring of free action' },
      { cn: '警告之戒', en: 'ring of warning' },
      { cn: '充能魔杖', en: 'wand of charging' },
      { cn: '严寒之杖', en: 'wand of cold' },
      { cn: '闪电之杖', en: 'wand of lightning' },
      { cn: '魔法飞弹之杖', en: 'wand of magic missile' }
    ],
    shopTypes: [
      { cn: '百货商店', en: 'general store' },
      { cn: '古兵器店', en: 'antique weapons outlet' },
      { cn: '二手盔甲店', en: 'used armour dealership' },
      { cn: '二手文具商店', en: 'second-hand stationery store' },
      { cn: '灯具店', en: 'lighting store' },
      { cn: '优质的服装及配饰', en: 'quality apparel and accessories' },
      { cn: '魔法书店', en: 'book store' },
      { cn: '稀有书籍', en: 'rare books' },
      { cn: '熟食店', en: 'delicatessen' },
      { cn: '酒类商场', en: 'liquor emporium' },
      { cn: '神圣森林', en: 'sacred grove' },
      { cn: '祭坛', en: 'holy shrine' },
      { cn: '黑暗圣殿', en: 'dark sepulchre' },
      { cn: '五金店', en: 'hardware store' },
      { cn: '枪店', en: 'gun mart' },
      { cn: '珠宝店', en: 'jewellers' },
      { cn: '制作站点', en: 'craft station' }
    ]
  };

  let cachedSystemCores = { maxCores: 8, defaultThreads: 4 };

  // Concurrency & Threads Configuration UI (SHPD Style)
  function initThreadsUI(maxCores, defaultThreads) {
    if (maxCores) cachedSystemCores.maxCores = maxCores;
    if (defaultThreads) cachedSystemCores.defaultThreads = defaultThreads;
    const selectEl = document.getElementById('filter-threads');
    const labelEl = document.getElementById('threads-indicator-label');
    if (!selectEl) return;

    const currentVal = parseInt(selectEl.value, 10);
    selectEl.innerHTML = '';
    const mCores = cachedSystemCores.maxCores;
    const def = currentVal || cachedSystemCores.defaultThreads || Math.max(1, mCores - 2);

    const isEn = !I18N.isZh();
    const configs = [];
    if (mCores >= 16) configs.push({ cores: mCores, label: isEn ? `🚀 Max Turbo (${mCores} threads - Full CPU)` : `🚀 满血狂飙 (${mCores} 线程 - 全核拉满)` });
    if (mCores >= 12) configs.push({ cores: Math.min(12, mCores), label: isEn ? `⚡ Super Speed (${Math.min(12, mCores)} threads - Recommended)` : `⚡ 极速全开 (${Math.min(12, mCores)} 线程 - 推荐最佳)` });
    if (mCores >= 8) configs.push({ cores: Math.min(8, mCores), label: isEn ? `⚖️ Balanced (${Math.min(8, mCores)} threads - Smooth)` : `⚖️ 平衡兼顾 (${Math.min(8, mCores)} 线程 - 适中流畅)` });
    if (mCores >= 4) configs.push({ cores: 4, label: isEn ? `🛡️ Light (4 threads)` : `🛡️ 轻量节能 (4 线程)` });
    if (mCores >= 2) configs.push({ cores: 2, label: isEn ? `🌱 Dual-Core (2 threads)` : `🌱 双核基础 (2 线程)` });
    configs.push({ cores: 1, label: isEn ? `Single Debug (1 thread)` : `单核调试 (1 线程)` });

    // Deduplicate configs by cores
    const seenCores = new Set();
    configs.forEach(cfg => {
      if (!seenCores.has(cfg.cores) && cfg.cores <= mCores) {
        seenCores.add(cfg.cores);
        const opt = document.createElement('option');
        opt.value = cfg.cores;
        opt.textContent = cfg.label;
        if (cfg.cores === def) opt.selected = true;
        selectEl.appendChild(opt);
      }
    });

    const updateLabel = () => {
      if (labelEl) labelEl.textContent = I18N.t('find.threadsFormat', selectEl.value);
    };
    selectEl.onchange = updateLabel;
    updateLabel();
  }

  // Asynchronously fetch suggestions & system threads from backend
  fetch('/api/suggestions')
    .then(r => r.ok ? r.json() : null)
    .then(data => {
      if (data) {
        if (Array.isArray(data.artifacts) && data.artifacts.length) suggestionsData.artifacts = data.artifacts;
        if (Array.isArray(data.sokobanPrizes) && data.sokobanPrizes.length) suggestionsData.sokobanPrizes = data.sokobanPrizes;
        if (Array.isArray(data.shopTypes) && data.shopTypes.length) suggestionsData.shopTypes = data.shopTypes;
        if (data.maxCores) {
          initThreadsUI(data.maxCores, data.defaultThreads);
        }
      }
    })
    .catch(() => {});

  // Utility to escape regex special characters
  function escapeRegExp(string) {
    return string.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  }

  // Highlight query within text
  function highlightMatch(text, query) {
    if (!query || !text) return text || '';
    const escaped = escapeRegExp(query);
    const regex = new RegExp(`(${escaped})`, 'gi');
    return text.replace(regex, '<span class="bg-brand-500/30 text-brand-300 font-bold px-0.5 rounded">$1</span>');
  }

  // Generic Reusable Autocomplete & Typeahead Dropdown Component
  function attachAutocomplete(inputEl, toggleBtn, dropdownEl, getItemsFn, onSelectFn, defaultIcon = '✨') {
    let activeIndex = -1;
    let filteredList = [];

    function renderDropdown(items, query) {
      filteredList = items;
      activeIndex = -1;
      dropdownEl.innerHTML = '';

      if (!items || items.length === 0) {
        dropdownEl.innerHTML = `
          <div class="px-3 py-2.5 text-center text-slate-500 text-xs italic">
            ${I18N.isZh() ? `未找到包含 "${query}" 的候选项` : `No suggestions found for "${query}"`}
          </div>
        `;
        dropdownEl.classList.remove('hidden');
        return;
      }

      // Sticky header showing count and hint
      const headerEl = document.createElement('div');
      headerEl.className = 'sticky top-0 bg-surface-950/95 backdrop-blur-md px-3 py-1.5 border-b border-surface-800 text-[10px] text-slate-400 flex items-center justify-between select-none z-10';
      if (!query) {
        headerEl.innerHTML = `
          <span class="font-semibold text-slate-300">${I18N.isZh() ? `📋 全部名录 (共 ${items.length} 项)` : `📋 All Items (${items.length})`}</span>
          <span class="text-amber-400/80 font-mono">${I18N.isZh() ? '可上下滚动浏览' : 'Scroll to browse'}</span>
        `;
      } else {
        headerEl.innerHTML = `
          <span class="font-semibold text-slate-300">${I18N.isZh() ? `🔍 匹配结果 (${items.length} 项)` : `🔍 Matches (${items.length})`}</span>
          <span class="text-brand-400/80 font-mono">${I18N.isZh() ? '按回车 ↵ 填入' : 'Press Enter ↵'}</span>
        `;
      }
      dropdownEl.appendChild(headerEl);

      const isEn = !I18N.isZh();

      items.forEach((item, idx) => {
        const itemEl = document.createElement('div');
        itemEl.className = 'autocomplete-item px-3 py-2 flex items-center justify-between cursor-pointer hover:bg-surface-800 text-xs transition border-b border-surface-800/40 last:border-b-0';
        itemEl.setAttribute('data-index', idx);

        const mainName = isEn ? (item.en || item.cn || '') : (item.cn || item.en || '');
        const subName = isEn ? (item.cn || '') : (item.en || '');
        const mainDisplay = highlightMatch(mainName, query);
        const subDisplay = subName ? highlightMatch(subName, query) : '';

        const glyphName = item.glyph || item.en || '';
        const tileUrl = glyphName ? getTileUrl(glyphName) : '';

        const iconHtml = tileUrl ? `
          <div class="w-6 h-6 rounded bg-surface-950/80 p-0.5 border border-surface-700/60 flex items-center justify-center shrink-0 shadow-xs">
            <img src="${tileUrl}" 
                 class="autocomplete-tile-img w-5 h-5 object-contain" 
                 data-glyph="${glyphName}" 
                 alt="${mainName}" 
                 onerror="this.style.display='none'; this.nextElementSibling.classList.remove('hidden');">
            <span class="text-xs hidden select-none">${defaultIcon}</span>
          </div>
        ` : `
          <div class="w-6 h-6 rounded bg-surface-950/80 p-0.5 border border-surface-700/60 flex items-center justify-center shrink-0 shadow-xs">
            <span class="text-xs select-none">${defaultIcon}</span>
          </div>
        `;

        itemEl.innerHTML = `
          <div class="flex items-center gap-2.5 min-w-0">
            ${iconHtml}
            <div class="truncate">
              <span class="font-semibold text-slate-100">${mainDisplay}</span>
              ${subName ? `<span class="text-[11px] text-slate-400 font-mono ml-1.5">(${subDisplay})</span>` : ''}
            </div>
          </div>
          <span class="text-[10px] text-brand-400 font-mono opacity-80 shrink-0">${isEn ? 'Select ↵' : '选择 ↵'}</span>
        `;

        itemEl.addEventListener('mousedown', (e) => {
          e.preventDefault();
          selectItem(item);
        });

        itemEl.addEventListener('mouseenter', () => {
          updateActiveHighlight(idx);
        });

        dropdownEl.appendChild(itemEl);
      });

      dropdownEl.classList.remove('hidden');
    }

    function updateActiveHighlight(newIdx) {
      activeIndex = newIdx;
      const items = dropdownEl.querySelectorAll('.autocomplete-item');
      items.forEach((el, idx) => {
        if (idx === activeIndex) {
          el.classList.add('bg-brand-500/20', 'border-l-2', 'border-l-brand-400');
          el.scrollIntoView({ block: 'nearest' });
        } else {
          el.classList.remove('bg-brand-500/20', 'border-l-2', 'border-l-brand-400');
        }
      });
    }

    function selectItem(item) {
      const chosen = I18N.isZh() ? (item.cn || item.en || item) : (item.en || item.cn || item);
      inputEl.value = chosen;
      dropdownEl.classList.add('hidden');
      if (typeof onSelectFn === 'function') {
        onSelectFn(item, chosen);
      }
    }

    function triggerSearch(showAll = false) {
      const q = (inputEl.value || '').trim();
      const allItems = getItemsFn();
      if (!allItems) return;

      if (!q || showAll) {
        renderDropdown(allItems, '');
        return;
      }

      const qLower = q.toLowerCase();
      const matches = allItems.filter(it => {
        const cn = (it.cn || it || '').toLowerCase();
        const en = (it.en || '').toLowerCase();
        const alias = (it.alias || '').toLowerCase();
        return cn.includes(qLower) || en.includes(qLower) || (alias && alias.includes(qLower));
      });

      renderDropdown(matches, q);
    }

    // Input events
    inputEl.addEventListener('input', () => {
      triggerSearch(false);
    });

    inputEl.addEventListener('focus', () => {
      triggerSearch(false);
    });

    inputEl.addEventListener('keydown', (e) => {
      if (dropdownEl.classList.contains('hidden')) {
        if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
          triggerSearch(true);
          e.preventDefault();
        }
        return;
      }

      if (e.key === 'ArrowDown') {
        e.preventDefault();
        if (filteredList.length > 0) {
          const next = (activeIndex + 1) % filteredList.length;
          updateActiveHighlight(next);
        }
      } else if (e.key === 'ArrowUp') {
        e.preventDefault();
        if (filteredList.length > 0) {
          const prev = (activeIndex - 1 + filteredList.length) % filteredList.length;
          updateActiveHighlight(prev);
        }
      } else if (e.key === 'Enter') {
        e.preventDefault();
        if (activeIndex >= 0 && activeIndex < filteredList.length) {
          selectItem(filteredList[activeIndex]);
        } else if (filteredList.length > 0) {
          selectItem(filteredList[0]);
        }
      } else if (e.key === 'Escape') {
        dropdownEl.classList.add('hidden');
      }
    });

    // Toggle button
    if (toggleBtn) {
      toggleBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        if (dropdownEl.classList.contains('hidden')) {
          inputEl.focus();
          triggerSearch(true);
        } else {
          dropdownEl.classList.add('hidden');
        }
      });
    }

    // Close on click outside
    document.addEventListener('click', (e) => {
      if (!inputEl.contains(e.target) && !dropdownEl.contains(e.target) && (!toggleBtn || !toggleBtn.contains(e.target))) {
        dropdownEl.classList.add('hidden');
      }
    });
  }
  const rulesContainer = document.getElementById('rules-container');
  const btnAddRule = document.getElementById('btn-add-rule');
  const btnSavePreset = document.getElementById('btn-save-preset');
  const presetsContainer = document.getElementById('presets-container');
  const rulesCountBadge = document.getElementById('rules-count-badge');
  const btnClearResults = document.getElementById('btn-clear-results');

  // Preview elements
  const previewPlaceholder = document.getElementById('preview-placeholder');
  const previewContent = document.getElementById('preview-content');
  const previewSeedCode = document.getElementById('preview-seed-code');
  const previewSeedSubtitle = document.getElementById('preview-seed-subtitle');
  const previewCopyBtn = document.getElementById('preview-copy-btn');
  const previewAnalyzeBtn = document.getElementById('preview-analyze-btn');
  const previewMatchedTags = document.getElementById('preview-matched-tags');
  const previewMinetown = document.getElementById('preview-minetown');
  const previewElf = document.getElementById('preview-elf');
  const previewSokoban = document.getElementById('preview-sokoban');
  const previewArtifactsList = document.getElementById('preview-artifacts-list');
  const previewArtifactCountBadge = document.getElementById('preview-artifact-count-badge');

  let conditionRules = [];
  let currentMatches = [];
  let activePreviewSeed = null;

  // Condition Rules Management
  function createDefaultRule(type = 'artifact') {
    const id = 'rule_' + Date.now() + '_' + Math.random().toString(36).substr(2, 5);
    return {
      id,
      type,
      logicRole: 'must', // 'must' (必选/AND) or 'optional' (备选/OR)
      artifactName: '',
      maxLevel: '',
      site: '',
      allowCarried: true,
      minetownState: 'orc',
      elfKingdomState: 'massacre',
      sokobanPrize: '',
      sokobanLevel: '',
      shopType: '',
      minShops: '',
      minArtifacts: ''
    };
  }

  function renderRulesList() {
    rulesContainer.innerHTML = '';
    rulesCountBadge.textContent = conditionRules.length;

    // Mixed mode status hint
    const hintLabel = document.getElementById('rules-hint-label');
    if (hintLabel) {
      const hasOptional = conditionRules.some(r => r.logicRole === 'optional');
      const hasMust = conditionRules.some(r => r.logicRole !== 'optional');
      if (hasOptional && hasMust) {
        hintLabel.textContent = I18N.t('find.mixedModeHint');
        hintLabel.className = 'text-[11px] text-amber-400 font-medium animate-pulse';
      } else {
        hintLabel.textContent = I18N.t('find.activeRulesHint');
        hintLabel.className = 'text-[11px] text-slate-500';
      }
    }

    if (conditionRules.length === 0) {
      rulesContainer.innerHTML = `
        <div class="text-center py-6 bg-surface-950/40 rounded-xl border border-surface-800 text-slate-500 text-xs">
          ${I18N.t('find.rulesEmpty')}
        </div>
      `;
      return;
    }

    conditionRules.forEach((rule, index) => {
      const isOptional = rule.logicRole === 'optional';
      const card = document.createElement('div');
      card.className = isOptional
        ? 'bg-surface-950/90 border border-amber-500/40 rounded-xl p-3 shadow-md space-y-2.5 transition hover:border-amber-400/60 shadow-amber-950/20'
        : 'bg-surface-950/80 border border-surface-800 rounded-xl p-3 shadow-md space-y-2.5 transition hover:border-surface-700';
      card.setAttribute('data-rule-id', rule.id);

      // Card Header: Condition Type + Logic Role Toggle + Remove Button
      const header = document.createElement('div');
      header.className = 'flex items-center justify-between gap-2 border-b border-surface-800/80 pb-2';
      header.innerHTML = `
        <div class="flex items-center gap-1.5 flex-1 min-w-0">
          <span class="text-[11px] font-mono ${isOptional ? 'text-amber-400 font-bold' : 'text-slate-400'}">#${index + 1}</span>
          <button type="button" class="rule-role-btn px-2 py-0.5 rounded-full text-[10px] font-bold transition flex items-center gap-1 cursor-pointer select-none border shrink-0 ${isOptional ? 'bg-amber-950/80 text-amber-300 border-amber-500/60 hover:bg-amber-900/90 shadow-2xs' : 'bg-emerald-950/80 text-emerald-300 border-emerald-500/60 hover:bg-emerald-900/90 shadow-2xs'}" title="${isOptional ? I18N.t('rule.roleOptionalTip') : I18N.t('rule.roleMustTip')}">
            <span class="w-1.5 h-1.5 rounded-full ${isOptional ? 'bg-amber-400' : 'bg-emerald-400'}"></span>
            <span class="role-text">${isOptional ? I18N.t('rule.roleOptional') : I18N.t('rule.roleMust')}</span>
          </button>
          <select class="rule-type-select bg-surface-900 border border-surface-700 rounded-lg px-2 py-1 text-xs text-slate-200 font-semibold focus:outline-none focus:border-brand-500 cursor-pointer">
            <option value="artifact" ${rule.type === 'artifact' ? 'selected' : ''}>${I18N.t('rule.typeArtifact')}</option>
            <option value="minetown" ${rule.type === 'minetown' ? 'selected' : ''}>${I18N.t('rule.typeMinetown')}</option>
            <option value="elf" ${rule.type === 'elf' ? 'selected' : ''}>${I18N.t('rule.typeElf')}</option>
            <option value="sokoban" ${rule.type === 'sokoban' ? 'selected' : ''}>${I18N.t('rule.typeSokoban')}</option>
            <option value="shop" ${rule.type === 'shop' ? 'selected' : ''}>${I18N.t('rule.typeShop')}</option>
            <option value="artifact_count" ${rule.type === 'artifact_count' ? 'selected' : ''}>${I18N.t('rule.typeArtifactCount')}</option>
          </select>
        </div>
        <button type="button" class="btn-remove-rule text-slate-500 hover:text-red-400 p-1 rounded hover:bg-surface-800 transition" title="${I18N.isZh() ? '移除此条件' : 'Remove condition'}">
          ✕
        </button>
      `;

      // Card Body: dynamic by type
      const body = document.createElement('div');
      body.className = 'rule-body space-y-2 text-xs';

      if (rule.type === 'artifact') {
        body.innerHTML = `
          <div>
            <label class="block text-[11px] text-slate-400 mb-1 flex items-center justify-between">
              <span>${I18N.t('rule.artNameLabel')}</span>
              <span class="text-[10px] text-brand-400 font-normal">${I18N.isZh() ? '支持即时补全 ▾' : 'Autocomplete ▾'}</span>
            </label>
            <div class="relative w-full flex items-center">
              <div class="rule-art-tile-badge absolute left-2 top-1/2 -translate-y-1/2 w-6 h-6 rounded bg-surface-950/80 p-0.5 border border-surface-700/60 flex items-center justify-center shrink-0 pointer-events-none z-10 shadow-xs">
                <img src="" class="autocomplete-tile-img w-5 h-5 object-contain hidden" data-glyph="" alt="icon">
                <span class="rule-art-default-emoji text-xs select-none">🗡️</span>
              </div>
              <input type="text" class="rule-art-name w-full bg-surface-900 border border-surface-700 rounded-lg pl-9 pr-8 py-1.5 text-slate-100 placeholder-slate-500 focus:outline-none focus:border-brand-500 font-sans" placeholder="${I18N.t('rule.artNamePlaceholder')}" value="${I18N.escapeHtml(rule.artifactName || '')}" autocomplete="off" spellcheck="false">
              <button type="button" class="rule-art-toggle absolute right-1.5 top-1/2 -translate-y-1/2 text-slate-500 hover:text-brand-400 p-1 rounded text-xs transition" title="${I18N.isZh() ? '展开全部神器列表' : 'Show all artifacts'}">
                ▾
              </button>
              <div class="rule-art-dropdown hidden absolute left-0 right-0 top-full mt-1.5 z-50 bg-surface-900/95 border border-surface-700/80 rounded-xl shadow-2xl backdrop-blur-md max-h-72 overflow-y-auto">
              </div>
            </div>
          </div>
          <div class="grid grid-cols-2 gap-2">
            <div>
              <label class="block text-[11px] text-slate-400 mb-1">${I18N.t('rule.maxLevelLabel')}</label>
              <input type="number" min="1" max="40" class="rule-max-level w-full bg-surface-900 border border-surface-700 rounded-lg px-2.5 py-1.5 text-slate-100 placeholder-slate-500 focus:outline-none focus:border-brand-500 font-mono" placeholder="${I18N.t('rule.maxLevelPlaceholder')}" value="${rule.maxLevel || ''}">
            </div>
            <div>
              <label class="block text-[11px] text-slate-400 mb-1">${I18N.t('rule.siteLabel')}</label>
              <select class="rule-site w-full bg-surface-900 border border-surface-700 rounded-lg px-2.5 py-1.5 text-slate-100 focus:outline-none focus:border-brand-500">
                <option value="" ${!rule.site ? 'selected' : ''}>${I18N.t('rule.siteAny')}</option>
                <option value="Dungeon" ${rule.site === 'Dungeon' ? 'selected' : ''}>${I18N.t('rule.siteDungeon')}</option>
                <option value="Mines" ${rule.site === 'Mines' ? 'selected' : ''}>${I18N.t('rule.siteMines')}</option>
                <option value="Elf Kingdom" ${rule.site === 'Elf Kingdom' ? 'selected' : ''}>${I18N.t('rule.siteElf')}</option>
                <option value="Sokoban" ${rule.site === 'Sokoban' ? 'selected' : ''}>${I18N.t('rule.siteSokoban')}</option>
                <option value="Fort Ludios" ${rule.site === 'Fort Ludios' ? 'selected' : ''}>${I18N.t('rule.siteLudios')}</option>
              </select>
            </div>
          </div>
          <div class="pt-1 flex items-center justify-between text-[11px] text-slate-300">
            <label class="inline-flex items-center gap-1.5 cursor-pointer select-none" title="${I18N.t('rule.allowCarriedTip')}">
              <input type="checkbox" class="rule-allow-carried rounded border-surface-700 bg-surface-900 text-brand-500 focus:ring-0 cursor-pointer" ${rule.allowCarried !== false ? 'checked' : ''}>
              <span class="font-medium">${I18N.t('rule.allowCarriedLabel')}</span>
            </label>
            <span class="text-[10px] text-slate-500 font-mono rule-carried-status">${rule.allowCarried !== false ? (I18N.isZh() ? '允许Boss/小怪持有' : 'Boss drops allowed') : (I18N.isZh() ? '仅限地面/宝箱' : 'Ground/Chest only')}</span>
          </div>
        `;
      } else if (rule.type === 'minetown') {
        body.innerHTML = `
          <div>
            <label class="block text-[11px] text-slate-400 mb-1">${I18N.t('rule.minetownStateLabel')}</label>
            <select class="rule-minetown-state w-full bg-surface-900 border border-surface-700 rounded-lg px-2.5 py-1.5 text-slate-100 focus:outline-none focus:border-brand-500 font-semibold">
              <option value="orc" ${rule.minetownState === 'orc' ? 'selected' : ''}>${I18N.t('rule.minetownStateOrc')}</option>
              <option value="normal" ${rule.minetownState === 'normal' ? 'selected' : ''}>${I18N.t('rule.minetownStateNormal')}</option>
            </select>
          </div>
        `;
      } else if (rule.type === 'elf') {
        body.innerHTML = `
          <div>
            <label class="block text-[11px] text-slate-400 mb-1">${I18N.t('rule.elfStateLabel')}</label>
            <select class="rule-elf-state w-full bg-surface-900 border border-surface-700 rounded-lg px-2.5 py-1.5 text-slate-100 focus:outline-none focus:border-brand-500 font-semibold">
              <option value="massacre" ${rule.elfKingdomState === 'massacre' ? 'selected' : ''}>${I18N.t('rule.elfStateMassacre')}</option>
              <option value="peaceful" ${rule.elfKingdomState === 'peaceful' ? 'selected' : ''}>${I18N.t('rule.elfStatePeaceful')}</option>
            </select>
          </div>
        `;
      } else if (rule.type === 'sokoban') {
        const prize1 = I18N.isZh() ? '收纳之袋' : 'bag of holding';
        const prize2 = I18N.isZh() ? '反射护符' : 'amulet of reflection';
        const prize3 = I18N.isZh() ? '反射斗篷' : 'cloak of reflection';
        const prize4 = I18N.isZh() ? '隐形戒指' : 'ring of invisibility';
        body.innerHTML = `
          <div>
            <label class="block text-[11px] text-slate-400 mb-1 flex items-center justify-between">
              <span>${I18N.t('rule.sokoPrizeLabel')}</span>
              <span class="text-[10px] text-brand-400 font-normal">${I18N.isZh() ? '支持即时补全 ▾' : 'Autocomplete ▾'}</span>
            </label>
            <div class="relative w-full">
              <input type="text" class="rule-sokoban-prize w-full bg-surface-900 border border-surface-700 rounded-lg px-2.5 py-1.5 pr-8 text-slate-100 placeholder-slate-500 focus:outline-none focus:border-brand-500" placeholder="${I18N.t('rule.sokoPrizePlaceholder')}" value="${I18N.escapeHtml(rule.sokobanPrize || '')}" autocomplete="off" spellcheck="false">
              <button type="button" class="rule-soko-toggle absolute right-1.5 top-1/2 -translate-y-1/2 text-slate-500 hover:text-brand-400 p-1 rounded text-xs transition" title="${I18N.isZh() ? '展开常见大奖列表' : 'Show common rewards'}">
                ▾
              </button>
              <div class="rule-soko-dropdown hidden absolute left-0 right-0 top-full mt-1.5 z-50 bg-surface-900/95 border border-surface-700/80 rounded-xl shadow-2xl backdrop-blur-md max-h-64 overflow-y-auto">
              </div>
            </div>
            <div class="flex flex-wrap gap-1 mt-1.5 text-[10px]">
              <span class="text-slate-500">${I18N.t('rule.quickFill')}</span>
              <button type="button" class="quick-prize-btn px-1.5 py-0.5 rounded bg-surface-800 text-amber-300 hover:bg-surface-700">${prize1}</button>
              <button type="button" class="quick-prize-btn px-1.5 py-0.5 rounded bg-surface-800 text-amber-300 hover:bg-surface-700">${prize2}</button>
              <button type="button" class="quick-prize-btn px-1.5 py-0.5 rounded bg-surface-800 text-amber-300 hover:bg-surface-700">${prize3}</button>
              <button type="button" class="quick-prize-btn px-1.5 py-0.5 rounded bg-surface-800 text-amber-300 hover:bg-surface-700">${prize4}</button>
            </div>
          </div>
        `;
      } else if (rule.type === 'shop') {
        body.innerHTML = `
          <div>
            <label class="block text-[11px] text-slate-400 mb-1 flex items-center justify-between">
              <span>${I18N.t('rule.shopTypeLabel')}</span>
              <span class="text-[10px] text-brand-400 font-normal">${I18N.isZh() ? '支持即时补全 ▾' : 'Autocomplete ▾'}</span>
            </label>
            <div class="relative w-full">
              <input type="text" class="rule-shop-type w-full bg-surface-900 border border-surface-700 rounded-lg px-2.5 py-1.5 pr-8 text-slate-100 placeholder-slate-500 focus:outline-none focus:border-brand-500 font-sans" placeholder="${I18N.t('rule.shopTypePlaceholder')}" value="${I18N.escapeHtml(rule.shopType || '')}" autocomplete="off" spellcheck="false">
              <button type="button" class="rule-shop-toggle absolute right-1.5 top-1/2 -translate-y-1/2 text-slate-500 hover:text-brand-400 p-1 rounded text-xs transition" title="${I18N.isZh() ? '展开全部设施列表' : 'Show all facilities'}">
                ▾
              </button>
              <div class="rule-shop-dropdown hidden absolute left-0 right-0 top-full mt-1.5 z-50 bg-surface-900/95 border border-surface-700/80 rounded-xl shadow-2xl backdrop-blur-md max-h-72 overflow-y-auto">
              </div>
            </div>
          </div>
          <div class="grid grid-cols-2 gap-2">
            <div>
              <label class="block text-[11px] text-slate-400 mb-1">${I18N.t('rule.maxLevelLabel')}</label>
              <input type="number" min="1" max="40" class="rule-shop-max-level w-full bg-surface-900 border border-surface-700 rounded-lg px-2.5 py-1.5 text-slate-100 placeholder-slate-500 focus:outline-none focus:border-brand-500 font-mono" placeholder="${I18N.t('rule.maxLevelPlaceholder')}" value="${rule.maxLevel || ''}">
            </div>
            <div>
              <label class="block text-[11px] text-slate-400 mb-1">${I18N.t('rule.siteLabel')}</label>
              <select class="rule-shop-site w-full bg-surface-900 border border-surface-700 rounded-lg px-2.5 py-1.5 text-slate-100 focus:outline-none focus:border-brand-500">
                <option value="" ${!rule.site ? 'selected' : ''}>${I18N.t('rule.siteAny')}</option>
                <option value="Dungeon" ${rule.site === 'Dungeon' ? 'selected' : ''}>${I18N.t('rule.siteDungeon')}</option>
                <option value="Mines" ${rule.site === 'Mines' ? 'selected' : ''}>${I18N.t('rule.siteMines')}</option>
                <option value="Underdeep" ${rule.site === 'Underdeep' ? 'selected' : ''}>${I18N.isZh() ? '地城 (Underdeep)' : 'Underdeep'}</option>
                <option value="Elf Kingdom" ${rule.site === 'Elf Kingdom' ? 'selected' : ''}>${I18N.t('rule.siteElf')}</option>
              </select>
            </div>
          </div>
        `;
      } else if (rule.type === 'artifact_count') {
        body.innerHTML = `
          <div>
            <label class="block text-[11px] text-slate-400 mb-1">${I18N.t('rule.minArtifactsLabel')}</label>
            <input type="number" min="1" max="40" class="rule-min-artifacts w-full bg-surface-900 border border-surface-700 rounded-lg px-2.5 py-1.5 text-slate-100 placeholder-slate-500 focus:outline-none focus:border-brand-500 font-mono" placeholder="18" value="${rule.minArtifacts || ''}">
            <p class="text-[10px] text-slate-500 mt-1">${I18N.t('rule.minArtifactsTip')}</p>
          </div>
        `;
      }

      card.appendChild(header);
      card.appendChild(body);

      // Event Listeners for inputs
      const typeSelect = header.querySelector('.rule-type-select');
      typeSelect.addEventListener('change', (e) => {
        rule.type = e.target.value;
        renderRulesList();
      });

      const removeBtn = header.querySelector('.btn-remove-rule');
      removeBtn.addEventListener('click', () => {
        conditionRules = conditionRules.filter(r => r.id !== rule.id);
        renderRulesList();
      });

      const roleBtn = header.querySelector('.rule-role-btn');
      if (roleBtn) {
        roleBtn.addEventListener('click', (e) => {
          e.stopPropagation();
          rule.logicRole = (rule.logicRole === 'optional') ? 'must' : 'optional';
          renderRulesList();
        });
      }

      // Bind input fields back to rule object and attach autocompletes
      if (rule.type === 'artifact') {
        const nameInput = body.querySelector('.rule-art-name');
        const toggleBtn = body.querySelector('.rule-art-toggle');
        const dropdownEl = body.querySelector('.rule-art-dropdown');
        const maxLevelInput = body.querySelector('.rule-max-level');
        const siteSelect = body.querySelector('.rule-site');
        const badgeEl = body.querySelector('.rule-art-tile-badge');
        const badgeImg = badgeEl ? badgeEl.querySelector('img') : null;
        const badgeEmoji = badgeEl ? badgeEl.querySelector('.rule-art-default-emoji') : null;

        function updateBadgeIcon(val) {
          if (!badgeEl) return;
          const clean = (val || '').trim().toLowerCase();
          if (!clean) {
            if (badgeImg) { badgeImg.classList.add('hidden'); badgeImg.removeAttribute('data-glyph'); }
            if (badgeEmoji) badgeEmoji.classList.remove('hidden');
            return;
          }
          const found = suggestionsData.artifacts.find(a => 
            (a.cn && a.cn.toLowerCase() === clean) ||
            (a.en && a.en.toLowerCase() === clean) ||
            (a.alias && a.alias.toLowerCase() === clean) ||
            (a.cn && a.cn.toLowerCase().includes(clean)) ||
            (a.en && a.en.toLowerCase().includes(clean)) ||
            (a.alias && a.alias.toLowerCase().includes(clean))
          );
          if (found && (found.glyph || found.en)) {
            const g = found.glyph || found.en;
            if (badgeImg) {
              badgeImg.src = getTileUrl(g);
              badgeImg.setAttribute('data-glyph', g);
              badgeImg.classList.remove('hidden');
            }
            if (badgeEmoji) badgeEmoji.classList.add('hidden');
          } else {
            if (badgeImg) { badgeImg.classList.add('hidden'); badgeImg.removeAttribute('data-glyph'); }
            if (badgeEmoji) badgeEmoji.classList.remove('hidden');
          }
        }

        // Initialize badge if rule already has artifact name
        updateBadgeIcon(rule.artifactName);

        nameInput.addEventListener('input', (e) => {
          rule.artifactName = e.target.value;
          updateBadgeIcon(rule.artifactName);
        });
        maxLevelInput.addEventListener('input', (e) => { rule.maxLevel = e.target.value; });
        siteSelect.addEventListener('change', (e) => { rule.site = e.target.value; });

        const allowCarriedCheckbox = body.querySelector('.rule-allow-carried');
        if (allowCarriedCheckbox) {
          allowCarriedCheckbox.addEventListener('change', (e) => {
            rule.allowCarried = e.target.checked;
            const st = body.querySelector('.rule-carried-status');
            if (st) {
              st.textContent = e.target.checked 
                ? (I18N.isZh() ? '允许Boss/小怪持有' : 'Boss drops allowed') 
                : (I18N.isZh() ? '仅限地面/宝箱' : 'Ground/Chest only');
            }
          });
        }

        attachAutocomplete(nameInput, toggleBtn, dropdownEl, () => suggestionsData.artifacts, (item) => {
          rule.artifactName = item.cn || item;
          updateBadgeIcon(rule.artifactName);
        }, '🗡️');
      } else if (rule.type === 'minetown') {
        const mtSelect = body.querySelector('.rule-minetown-state');
        mtSelect.addEventListener('change', (e) => { rule.minetownState = e.target.value; });
      } else if (rule.type === 'elf') {
        const elfSelect = body.querySelector('.rule-elf-state');
        elfSelect.addEventListener('change', (e) => { rule.elfKingdomState = e.target.value; });
      } else if (rule.type === 'sokoban') {
        const prizeInput = body.querySelector('.rule-sokoban-prize');
        const toggleBtn = body.querySelector('.rule-soko-toggle');
        const dropdownEl = body.querySelector('.rule-soko-dropdown');

        prizeInput.addEventListener('input', (e) => { rule.sokobanPrize = e.target.value; });
        body.querySelectorAll('.quick-prize-btn').forEach(qb => {
          qb.addEventListener('click', () => {
            rule.sokobanPrize = qb.textContent.trim();
            prizeInput.value = rule.sokobanPrize;
          });
        });

        attachAutocomplete(prizeInput, toggleBtn, dropdownEl, () => suggestionsData.sokobanPrizes, (item) => {
          rule.sokobanPrize = item.cn || item;
        }, '📦');
      } else if (rule.type === 'shop') {
        const shopInput = body.querySelector('.rule-shop-type');
        const toggleBtn = body.querySelector('.rule-shop-toggle');
        const dropdownEl = body.querySelector('.rule-shop-dropdown');
        const maxLevelInput = body.querySelector('.rule-shop-max-level');
        const siteSelect = body.querySelector('.rule-shop-site');

        shopInput.addEventListener('input', (e) => { rule.shopType = e.target.value; });
        maxLevelInput.addEventListener('input', (e) => { rule.maxLevel = e.target.value; });
        siteSelect.addEventListener('change', (e) => { rule.site = e.target.value; });

        attachAutocomplete(shopInput, toggleBtn, dropdownEl, () => suggestionsData.shopTypes, (item) => {
          rule.shopType = item.cn || item;
        }, '🏪');
      } else if (rule.type === 'artifact_count') {
        const minArtInput = body.querySelector('.rule-min-artifacts');
        minArtInput.addEventListener('input', (e) => { rule.minArtifacts = e.target.value; });
      }

      rulesContainer.appendChild(card);
    });
  }

  // Add condition button
  btnAddRule.addEventListener('click', () => {
    conditionRules.push(createDefaultRule('artifact'));
    renderRulesList();
  });

  // Reset filters button
  btnResetFilters.addEventListener('click', () => {
    conditionRules = [createDefaultRule('artifact')];
    renderRulesList();
  });

  // -------------------------------------------------------------
  // Presets Handling (Built-in + Custom LocalStorage Presets)
  // -------------------------------------------------------------
  const PRESETS_STORAGE_KEY = 'pathos_seed_filter_custom_presets_v1';

  function getCustomPresets() {
    try {
      const raw = localStorage.getItem(PRESETS_STORAGE_KEY);
      return raw ? JSON.parse(raw) : [];
    } catch (e) {
      console.error('Failed to load custom presets from localStorage:', e);
      return [];
    }
  }

  function saveCustomPresets(list) {
    try {
      localStorage.setItem(PRESETS_STORAGE_KEY, JSON.stringify(list));
    } catch (e) {
      console.error('Failed to save custom presets to localStorage:', e);
    }
  }

  function getBuiltinPresets() {
    return [
      {
        id: 'builtin_high_artifacts',
        name: I18N.t('find.presetHighArt') || (I18N.isZh() ? '🏆 神器大满贯 (>=18件)' : '🏆 Grand Slam (>=18 Artifacts)'),
        isBuiltin: true,
        matchMode: 'all',
        rules: [
          { type: 'artifact_count', minArtifacts: 18 }
        ]
      }
    ];
  }

  function applyPreset(preset) {
    if (!preset || !preset.rules) return;
    conditionRules = preset.rules.map(r => ({
      ...createDefaultRule(r.type),
      ...r,
      id: 'rule_' + Date.now() + '_' + Math.random().toString(36).substr(2, 5)
    }));
    renderRulesList();
  }

  function renderPresetsContainer() {
    if (!presetsContainer) return;
    presetsContainer.innerHTML = '';

    const builtins = getBuiltinPresets();
    const customs = getCustomPresets();

    // 1. Render Builtin Presets (Only Grand Slam)
    builtins.forEach(p => {
      const btn = document.createElement('button');
      btn.type = 'button';
      btn.className = 'preset-btn px-2.5 py-1 rounded-lg bg-surface-900 hover:bg-surface-800 text-amber-300 border border-amber-500/30 font-medium transition active:scale-95 flex items-center gap-1 shadow-2xs';
      btn.innerHTML = `<span>${I18N.escapeHtml(p.name)}</span>`;
      btn.addEventListener('click', () => applyPreset(p));
      presetsContainer.appendChild(btn);
    });

    // 2. Render Custom Presets
    customs.forEach(p => {
      const group = document.createElement('div');
      group.className = 'custom-preset-group inline-flex items-center rounded-lg bg-surface-900 border border-surface-700/80 hover:border-brand-500/60 transition shadow-2xs overflow-hidden text-xs';

      const applyBtn = document.createElement('button');
      applyBtn.type = 'button';
      applyBtn.className = 'px-2 py-1 text-slate-200 hover:text-brand-300 font-medium transition flex items-center gap-1 active:bg-surface-800';
      applyBtn.innerHTML = `<span>📌</span><span class="max-w-[120px] truncate" title="${I18N.escapeHtml(p.name)}">${I18N.escapeHtml(p.name)}</span>`;
      applyBtn.addEventListener('click', () => applyPreset(p));

      const renameBtn = document.createElement('button');
      renameBtn.type = 'button';
      renameBtn.className = 'p-1 px-1.5 text-slate-500 hover:text-amber-400 border-l border-surface-800/80 hover:bg-surface-800 transition text-[11px]';
      renameBtn.setAttribute('title', I18N.t('find.promptPresetName') || 'Rename Preset');
      renameBtn.innerHTML = '✏️';
      renameBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        const newName = prompt(I18N.t('find.promptPresetName') || (I18N.isZh() ? '请输入预设新名称:' : 'Enter new preset name:'), p.name);
        if (newName && newName.trim() && newName.trim() !== p.name) {
          const list = getCustomPresets();
          const target = list.find(x => x.id === p.id);
          if (target) {
            target.name = newName.trim();
            saveCustomPresets(list);
            renderPresetsContainer();
          }
        }
      });

      const deleteBtn = document.createElement('button');
      deleteBtn.type = 'button';
      deleteBtn.className = 'p-1 px-1.5 text-slate-500 hover:text-red-400 border-l border-surface-800/80 hover:bg-surface-800 transition text-[11px]';
      deleteBtn.setAttribute('title', I18N.isZh() ? '删除预设' : 'Delete preset');
      deleteBtn.innerHTML = '✕';
      deleteBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        const confirmMsg = I18N.isZh() ? `确定要删除预设 "${p.name}" 吗？` : `Delete preset "${p.name}"?`;
        if (confirm(confirmMsg)) {
          let list = getCustomPresets();
          list = list.filter(x => x.id !== p.id);
          saveCustomPresets(list);
          renderPresetsContainer();
        }
      });

      group.appendChild(applyBtn);
      group.appendChild(renameBtn);
      group.appendChild(deleteBtn);
      presetsContainer.appendChild(group);
    });
  }

  // Save Current Filter Rules as Preset
  if (btnSavePreset) {
    btnSavePreset.addEventListener('click', () => {
      if (!conditionRules || conditionRules.length === 0) {
        alert(I18N.t('find.noRulesToSave') || (I18N.isZh() ? '当前没有任何激活的过滤条件，无法保存为预设！' : 'No active condition rules to save as preset!'));
        return;
      }
      const customs = getCustomPresets();
      const defaultName = I18N.isZh() ? `自定义预设 ${customs.length + 1}` : `Custom Preset ${customs.length + 1}`;
      const name = prompt(I18N.t('find.promptPresetName') || (I18N.isZh() ? '请输入此预设的名称:' : 'Enter preset name:'), defaultName);
      if (!name || !name.trim()) return;

      const newPreset = {
        id: 'preset_' + Date.now() + '_' + Math.random().toString(36).substr(2, 4),
        name: name.trim(),
        rules: conditionRules.map(r => {
          const c = { ...r };
          delete c.id;
          return c;
        })
      };
      customs.push(newPreset);
      saveCustomPresets(customs);
      renderPresetsContainer();
    });
  }

  // Initialize with 1 default condition
  conditionRules = [
    { ...createDefaultRule('artifact'), artifactName: I18N.isZh() ? '明镜' : 'Mirrorbright', maxLevel: 3, site: 'Dungeon' }
  ];
  renderRulesList();
  renderPresetsContainer();

  // Clear Results
  btnClearResults.addEventListener('click', () => {
    currentMatches = [];
    matchesContainer.innerHTML = `
      <div id="matches-empty" class="text-center py-16 bg-surface-900 border border-surface-800/80 rounded-2xl text-slate-500 space-y-2">
        <div class="text-3xl">🎯</div>
        <div class="text-sm font-medium" data-i18n="find.matchEmptyTitle">${I18N.t('find.matchEmptyTitle')}</div>
        <div class="text-xs text-slate-600" data-i18n="find.matchEmptyDesc">${I18N.t('find.matchEmptyDesc')}</div>
      </div>
    `;
    matchesCountBadge.textContent = '0';
    statMatched.textContent = '0';
    previewPlaceholder.classList.remove('hidden');
    previewContent.classList.add('hidden');
    activePreviewSeed = null;
  });

  // Start Search (SSE Stream)
  btnSearchStart.addEventListener('click', () => {
    if (eventSource) {
      eventSource.close();
      eventSource = null;
    }

    // Clean rules payload
    const cleanRules = conditionRules.map(r => {
      const obj = { type: r.type, logicRole: r.logicRole || 'must' };
      if (r.type === 'artifact') {
        if (r.artifactName) obj.artifactName = r.artifactName.trim();
        if (r.maxLevel) obj.maxLevel = parseInt(r.maxLevel, 10);
        if (r.site) obj.site = r.site;
        obj.allowCarried = r.allowCarried !== false;
      } else if (r.type === 'minetown') {
        obj.minetownState = r.minetownState;
      } else if (r.type === 'elf') {
        obj.elfKingdomState = r.elfKingdomState;
      } else if (r.type === 'sokoban') {
        if (r.sokobanPrize) obj.sokobanPrize = r.sokobanPrize.trim();
        if (r.sokobanLevel) obj.sokobanLevel = parseInt(r.sokobanLevel, 10);
      } else if (r.type === 'shop') {
        if (r.shopType) obj.shopType = r.shopType.trim();
        if (r.maxLevel) obj.maxLevel = parseInt(r.maxLevel, 10);
        if (r.site) obj.site = r.site;
        if (r.minShops) obj.minShops = parseInt(r.minShops, 10);
      } else if (r.type === 'artifact_count') {
        if (r.minArtifacts) obj.minArtifacts = parseInt(r.minArtifacts, 10);
      }
      return obj;
    });

    const params = new URLSearchParams();
    params.set('matchMode', 'all');
    params.set('rules', JSON.stringify(cleanRules));

    const max = document.getElementById('filter-max').value;
    if (max) params.set('max', max);
    const start = document.getElementById('filter-start').value;
    if (start) params.set('start', start);

    const threadsInput = document.getElementById('filter-threads');
    const activeThreads = threadsInput ? parseInt(threadsInput.value, 10) || 12 : 12;
    params.set('threads', activeThreads);

    // UI State for running
    btnSearchStart.disabled = true;
    btnSearchStart.classList.add('opacity-50');
    btnSearchStop.disabled = false;
    searchSpinner.className = 'inline-block w-3 h-3 rounded-full bg-amber-500 animate-pulse';
    searchStatusText.textContent = I18N.isZh()
      ? `正在以 ${activeThreads} 核心全速并发检索种子中...`
      : `Searching seeds concurrently with ${activeThreads} worker threads...`;
    searchProgressBar.style.width = '0%';
    matchesContainer.innerHTML = '';
    matchesCountBadge.textContent = '0';
    statTested.textContent = '0';
    statMatched.textContent = '0';
    statSpeed.textContent = I18N.isZh() ? `⚡ 启动中... (${activeThreads}核)` : `⚡ Starting... (${activeThreads}T)`;
    statCurrentSeed.textContent = '--';
    currentMatches = [];

    searchStartTime = performance.now();
    const targetMax = parseInt(max, 10) || 100;
    let matchCount = 0;

    eventSource = new EventSource(`/api/filter/stream?${params.toString()}`);

    eventSource.onmessage = (event) => {
      try {
        const data = JSON.parse(event.data);
        if (data.type === 'progress') {
          statTested.textContent = data.tested;
          statMatched.textContent = data.matched;
          statCurrentSeed.textContent = data.currentSeed;
          const pct = Math.min(100, Math.round((data.tested / targetMax) * 100));
          searchProgressBar.style.width = `${pct}%`;

          const unit = I18N.t('find.speedUnit');
          const threadLabel = I18N.isZh() ? '核' : 'T';
          if (data.speed !== undefined && data.speed > 0) {
            statSpeed.textContent = `⚡ ${data.speed} ${unit} (${data.threads || activeThreads}${threadLabel})`;
          } else {
            const elapsedSec = (performance.now() - searchStartTime) / 1000;
            if (elapsedSec > 0.2) {
              statSpeed.textContent = `⚡ ${(data.tested / elapsedSec).toFixed(1)} ${unit} (${activeThreads}${threadLabel})`;
            }
          }
        } else if (data.type === 'match') {
          matchCount++;
          matchesCountBadge.textContent = matchCount;
          statMatched.textContent = matchCount;
          currentMatches.push(data.report);
          appendMatchCard(data.report);

          // Auto preview the first match
          if (matchCount === 1) {
            selectMatchForPreview(data.report);
          }
        } else if (data.type === 'done') {
          finishSearch(data.totalMatches, data.totalTested, data.speed);
        }
      } catch (err) {
        console.error('Error parsing SSE event:', err);
      }
    };

    eventSource.onerror = (err) => {
      console.warn('EventSource disconnected or completed', err);
      finishSearch(matchCount, targetMax);
    };
  });

  // Global Ctrl+Enter shortcut to toggle find
  window.addEventListener('keydown', (e) => {
    if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') {
      const findView = document.getElementById('view-find');
      if (findView && !findView.classList.contains('hidden')) {
        e.preventDefault();
        if (btnSearchStart.disabled) {
          stopSearch();
        } else {
          btnSearchStart.click();
        }
      }
    }
  });

  function stopSearch() {
    if (eventSource) {
      eventSource.close();
      eventSource = null;
    }
    btnSearchStart.disabled = false;
    btnSearchStart.classList.remove('opacity-50');
    btnSearchStop.disabled = true;
    searchSpinner.className = 'inline-block w-3 h-3 rounded-full bg-slate-500';
    searchStatusText.textContent = I18N.t('toast.searchStopped');
  }

  btnSearchStop.addEventListener('click', stopSearch);

  function finishSearch(matches, tested, speed) {
    stopSearch();
    const speedInfo = speed ? (I18N.isZh() ? ` (均速 ${speed} 种/s)` : ` (avg ${speed} seeds/s)`) : '';
    searchStatusText.textContent = I18N.isZh()
      ? `检索完成！已并发测试 ${tested} 个种子，找到 ${matches} 个匹配！${speedInfo}`
      : `Search complete! Tested ${tested} seeds, found ${matches} matches!${speedInfo}`;
    searchProgressBar.style.width = '100%';
    if (matches === 0) {
      matchesContainer.innerHTML = `
        <div class="text-center py-12 bg-surface-900 border border-surface-800 rounded-2xl text-slate-400">
          <div class="text-2xl mb-1">🔍</div>
          <div>${I18N.isZh() ? `未能在测试的 ${tested} 个种子中找到完全匹配的项` : `No matches found among the ${tested} tested seeds`}</div>
          <div class="text-xs text-slate-500 mt-1">${I18N.isZh() ? '建议适当放宽深度或切换为“任一满足 (Match Any)”模式' : 'Try relaxing constraints or switching to "Match Any" mode'}</div>
        </div>
      `;
    }
  }

  // Render Result Row
  function appendMatchCard(report) {
    const card = document.createElement('div');
    card.className = 'match-result-row group relative bg-surface-900 border border-surface-800 hover:border-brand-500/80 rounded-xl p-3 shadow-md transition cursor-pointer select-none';
    card.setAttribute('data-seed', report.seedText);

    // Matched tags summary
    const reasons = (report.matchedReasons && report.matchedReasons.length > 0)
      ? report.matchedReasons.slice(0, 3).map(r => {
          const isOpt = r.includes('[备选]') || r.includes('[Optional]');
          const bgCls = isOpt
            ? 'bg-amber-950/80 text-amber-300 border-amber-700/80'
            : 'bg-emerald-950/80 text-emerald-400 border-emerald-800';
          return `<span class="inline-block px-1.5 py-0.5 rounded ${bgCls} border text-[10px] font-medium">${I18N.escapeHtml(r)}</span>`;
        }).join('')
      : '<span class="inline-block px-1.5 py-0.5 rounded bg-surface-950 text-amber-400 border border-surface-800 text-[10px]">✓ 符合条件</span>';

    // Top 3 artifact sprites
    const isEn = I18N.getLanguage() === 'en';
    const artSprites = (report.artifacts || []).slice(0, 3).map(a => `
      <img src="${getTileUrl(a.glyph || a.englishName)}" class="w-4 h-4 object-contain inline-block shrink-0 rounded bg-surface-950 p-0.5" title="${isEn ? a.englishName : a.chineseName} (D${a.dungeonLevel})" onerror="this.style.display='none'">
    `).join('');

    card.innerHTML = `
      <div class="flex items-center justify-between gap-2 mb-1.5">
        <div class="flex items-center gap-1.5 min-w-0">
          <span class="font-mono text-sm font-bold text-amber-400 truncate">${report.seedText}</span>
          <span class="text-[10px] px-1 rounded bg-surface-950 text-slate-400 font-mono">D1-40</span>
        </div>
        <div class="flex items-center gap-1">
          <button type="button" class="btn-row-copy p-1 rounded hover:bg-surface-800 text-slate-400 hover:text-white text-xs transition" title="${isEn ? 'Copy Seed' : '复制种子'}">
            📋
          </button>
          <button type="button" class="btn-row-analyze p-1 rounded hover:bg-surface-800 text-amber-400 hover:text-amber-300 text-xs transition font-bold" title="${isEn ? 'Analyze Seed' : '深入透视'}">
            🔍
          </button>
        </div>
      </div>

      <div class="flex flex-wrap items-center gap-1 mb-2">
        ${reasons}
      </div>

      <div class="flex items-center justify-between text-[11px] text-slate-400 border-t border-surface-800/60 pt-1.5">
        <div class="flex items-center gap-1">
          <span class="text-slate-500">${isEn ? 'Artifacts:' : '神器:'}</span>
          ${artSprites || `<span class="text-slate-600">${isEn ? 'None' : '无'}</span>`}
          <span class="text-[10px] text-amber-300 font-mono">(${report.totalArtifacts})</span>
        </div>
        <div class="text-slate-500 text-[10px] font-mono">
          ${report.elapsedMs}ms
        </div>
      </div>
    `;

    // Click whole row to preview
    card.addEventListener('click', (e) => {
      if (e.target.closest('.btn-row-copy') || e.target.closest('.btn-row-analyze')) return;
      selectMatchForPreview(report);
    });

    // Copy button
    card.querySelector('.btn-row-copy').addEventListener('click', (e) => {
      e.stopPropagation();
      navigator.clipboard.writeText(report.seedText).then(() => {
        const btn = e.currentTarget;
        btn.textContent = '✓';
        setTimeout(() => { btn.textContent = '📋'; }, 1000);
      });
    });

    // Analyze button
    card.querySelector('.btn-row-analyze').addEventListener('click', (e) => {
      e.stopPropagation();
      openInAnalyzeMode(report.seedText);
    });

    matchesContainer.appendChild(card);
  }

  // Switch to analyze mode and load seed
  function openInAnalyzeMode(seedText) {
    switchMode('analyze');
    analyzeSeedInput.value = seedText;
    runAnalyze(seedText);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  // In-place Seed Preview
  function selectMatchForPreview(report) {
    activePreviewSeed = report;
    const isEn = I18N.getLanguage() === 'en';

    // Highlight row
    document.querySelectorAll('.match-result-row').forEach(row => {
      if (row.getAttribute('data-seed') === report.seedText) {
        row.classList.add('border-brand-500', 'bg-surface-850', 'ring-1', 'ring-brand-500/50');
      } else {
        row.classList.remove('border-brand-500', 'bg-surface-850', 'ring-1', 'ring-brand-500/50');
      }
    });

    previewPlaceholder.classList.add('hidden');
    previewContent.classList.remove('hidden');

    previewSeedCode.textContent = report.seedText;
    previewSeedSubtitle.textContent = isEn
      ? `Code: ${report.seedCode} · Time: ${report.elapsedMs}ms · Total Artifacts: ${report.totalArtifacts}`
      : `数值代码: ${report.seedCode} · 模拟耗时: ${report.elapsedMs}ms · 全局神器: ${report.totalArtifacts}件`;

    // Copy & Analyze actions
    previewCopyBtn.onclick = () => {
      navigator.clipboard.writeText(report.seedText).then(() => {
        previewCopyBtn.textContent = I18N.t('find.previewCopied');
        setTimeout(() => { previewCopyBtn.textContent = I18N.t('find.previewCopy'); }, 1200);
      });
    };
    previewAnalyzeBtn.onclick = () => {
      openInAnalyzeMode(report.seedText);
    };

    // Matched Tags
    if (report.matchedReasons && report.matchedReasons.length > 0) {
      previewMatchedTags.innerHTML = report.matchedReasons.map(r => {
        const isOpt = r.includes('[备选]') || r.includes('[Optional]');
        const bgCls = isOpt
          ? 'bg-amber-950/80 text-amber-300 border-amber-600/70 shadow-amber-950/20'
          : 'bg-emerald-950/80 text-emerald-300 border-emerald-700/60';
        const icon = isOpt ? '⚡' : '🎯';
        return `
          <span class="inline-flex items-center gap-1 px-2.5 py-1 rounded-lg ${bgCls} border text-xs font-medium shadow-sm">
            <span>${icon}</span>
            <span>${I18N.escapeHtml(r)}</span>
          </span>
        `;
      }).join('');
    } else {
      previewMatchedTags.innerHTML = `
        <span class="inline-flex items-center gap-1 px-2.5 py-1 rounded-lg bg-emerald-950/80 text-emerald-300 border border-emerald-700/60 text-xs font-medium">
          <span>✓</span>
          <span>${I18N.t('find.previewAllMatched')}</span>
        </span>
      `;
    }

    // Macro Cards
    if (report.minetown) {
      previewMinetown.textContent = report.minetown.isOrcSiege 
        ? (isEn ? '⚔️ Siege' : '⚔️ 兽人攻城') 
        : (isEn ? '🛡️ Peaceful' : '🛡️ 守卫巡逻');
      previewMinetown.className = report.minetown.isOrcSiege ? 'font-bold text-red-400 mt-0.5 truncate text-[11px]' : 'font-bold text-emerald-400 mt-0.5 truncate text-[11px]';
    } else {
      previewMinetown.textContent = I18N.t('analyze.minetownNone');
      previewMinetown.className = 'font-bold text-slate-500 mt-0.5 truncate text-[11px]';
    }

    if (report.elfKingdom) {
      previewElf.textContent = report.elfKingdom.isMassacre 
        ? (isEn ? '🔥 Rebellion' : '🔥 屠城惨案') 
        : (isEn ? '🌿 Peaceful' : '🌿 盛世繁荣');
      previewElf.className = report.elfKingdom.isMassacre ? 'font-bold text-red-400 mt-0.5 truncate text-[11px]' : 'font-bold text-emerald-400 mt-0.5 truncate text-[11px]';
    } else {
      previewElf.textContent = I18N.t('analyze.elfNone');
      previewElf.className = 'font-bold text-slate-500 mt-0.5 truncate text-[11px]';
    }

    if (report.sokoban) {
      previewSokoban.textContent = isEn ? `Level ${report.sokoban.dungeonEntranceLevel} Entrance` : `第 ${report.sokoban.dungeonEntranceLevel} 层入口`;
    } else {
      previewSokoban.textContent = isEn ? 'Not found' : '未探测';
    }

    // Early Floors Artifacts Timeline (<= 10 floors)
    const earlyArtifacts = (report.artifacts || [])
      .filter(a => a.dungeonLevel <= 10)
      .sort((a, b) => a.dungeonLevel - b.dungeonLevel);

    previewArtifactCountBadge.textContent = I18N.t('find.previewEarlyCount', earlyArtifacts.length);

    if (earlyArtifacts.length === 0) {
      previewArtifactsList.innerHTML = `
        <div class="text-center py-6 text-slate-500 text-xs bg-surface-950/50 rounded-xl border border-surface-800">
          ${I18N.t('find.previewEarlyEmpty')}
        </div>
      `;
    } else {
      previewArtifactsList.innerHTML = earlyArtifacts.map(art => {
        const iconUrl = getTileUrl(art.glyph || art.englishName);
        const possessionBadge = I18N.formatPossessionBadge(art.possession, art.isLocked, art.isTrapped);
        const nameHtml = isEn
          ? `<div class="font-bold text-slate-200 truncate font-mono">${art.englishName}</div>`
          : `<div class="font-bold text-slate-200 truncate">${art.chineseName}</div><div class="text-[10px] text-slate-500 font-mono truncate">${art.englishName}</div>`;
        const siteName = isEn ? (art.site || art.siteChinese) : (art.siteChinese || art.site);

        return `
          <div class="flex items-center justify-between p-2 rounded-xl bg-surface-950/70 border border-surface-800/80 text-xs hover:border-surface-700 transition">
            <div class="flex items-center gap-2 min-w-0">
              <span class="px-1.5 py-0.5 rounded bg-surface-900 text-amber-400 font-mono font-bold text-[10px] shrink-0">
                D${art.dungeonLevel}
              </span>
              <img src="${iconUrl}" class="w-5 h-5 object-contain shrink-0" onerror="this.style.display='none'">
              <div class="min-w-0">
                ${nameHtml}
              </div>
            </div>
            <div class="flex items-center gap-1.5 shrink-0 text-[10px]">
              <span class="text-slate-400">${siteName}</span>
              ${possessionBadge}
            </div>
          </div>
        `;
      }).join('');
    }
  }

  // -------------------------------------------------------------
  // Map Viewer Module (Interactive HTML5 Canvas)
  // -------------------------------------------------------------
  let currentMapCatalog = [];
  let currentFloorData = null;
  const currentTileMap = new Map(); // "x,y" => tile
  const currentShopMap = new Map(); // "x,y" => shop
  let mapScale = 1.0;
  let mapOffsetX = 0;
  let mapOffsetY = 0;
  let isMapDragging = false;
  let dragLastX = 0;
  let dragLastY = 0;
  let hoveredTile = null;

  let showTextures = true;
  let showItems = true;
  let showShops = true;
  let showBoulders = true;
  let showTraps = true;
  let showStairs = true;
  let showFixtures = true;
  let showSecrets = true;

  const currentItemMap = new Map();

  const mapFloorSelect = document.getElementById('map-floor-select');
  const mapCanvasContainer = document.getElementById('map-canvas-container');
  const mapCanvas = document.getElementById('map-canvas');
  const mapCtx = mapCanvas ? mapCanvas.getContext('2d') : null;
  const mapTooltip = document.getElementById('map-tooltip');
  const mapSizeBadge = document.getElementById('map-size-badge');
  const mapCoordsBadge = document.getElementById('map-coords-badge');
  const mapZoomLabel = document.getElementById('map-zoom-label');
  const mapLoadingOverlay = document.getElementById('map-loading-overlay');

  function getTileImage(glyphName) {
    if (!glyphName) return null;
    const cacheKey = `${currentTileset}:${glyphName.toLowerCase()}`;
    if (tileImageCache.has(cacheKey)) {
      return tileImageCache.get(cacheKey);
    }
    const img = new Image();
    img.src = getTileUrl(glyphName);
    img.onload = () => {
      tileImageCache.set(cacheKey, img);
      requestAnimationFrame(renderMapCanvas);
    };
    img.onerror = () => {
      tileImageCache.set(cacheKey, null);
    };
    tileImageCache.set(cacheKey, null);
    return null;
  }

  function resizeMapCanvas() {
    if (!mapCanvasContainer || !mapCanvas || !mapCtx) return;
    const rect = mapCanvasContainer.getBoundingClientRect();
    if (rect.width === 0 || rect.height === 0) return;
    const dpr = window.devicePixelRatio || 1;
    mapCanvas.width = rect.width * dpr;
    mapCanvas.height = rect.height * dpr;
    mapCtx.setTransform(1, 0, 0, 1, 0, 0);
    mapCtx.scale(dpr, dpr);
    renderMapCanvas();
  }
  window.addEventListener('resize', resizeMapCanvas);

  function resetMapViewport() {
    if (!currentFloorData || !mapCanvasContainer) return;
    const rect = mapCanvasContainer.getBoundingClientRect();
    if (rect.width === 0 || rect.height === 0) return;

    const mapPixelW = currentFloorData.width * baseTileSize;
    const mapPixelH = currentFloorData.height * baseTileSize;

    const padding = 36;
    const scaleX = (rect.width - padding * 2) / mapPixelW;
    const scaleY = (rect.height - padding * 2) / mapPixelH;
    mapScale = Math.min(Math.max(Math.min(scaleX, scaleY), 0.35), 2.0);

    mapOffsetX = (rect.width - mapPixelW * mapScale) / 2;
    mapOffsetY = (rect.height - mapPixelH * mapScale) / 2;

    if (mapZoomLabel) mapZoomLabel.textContent = `${Math.round(mapScale * 100)}%`;
    renderMapCanvas();
  }

  function renderMapCanvas() {
    if (!currentFloorData || !mapCanvas || !mapCtx || !mapCanvasContainer) return;
    const rect = mapCanvasContainer.getBoundingClientRect();
    if (rect.width === 0 || rect.height === 0) return;

    mapCtx.clearRect(0, 0, rect.width, rect.height);

    mapCtx.save();
    mapCtx.translate(mapOffsetX, mapOffsetY);
    mapCtx.scale(mapScale, mapScale);

    const mapW = currentFloorData.width * baseTileSize;
    const mapH = currentFloorData.height * baseTileSize;

    // 1. Dark background for map bounds
    mapCtx.fillStyle = '#0f172a';
    mapCtx.fillRect(0, 0, mapW, mapH);
    mapCtx.strokeStyle = 'rgba(51, 65, 85, 0.4)';
    mapCtx.lineWidth = 1;
    mapCtx.strokeRect(0, 0, mapW, mapH);

    // 2. Draw Tiles
    const tiles = currentFloorData.tiles || [];
    for (let i = 0; i < tiles.length; i++) {
      const t = tiles[i];
      const dx = t.x * baseTileSize;
      const dy = t.y * baseTileSize;

      // Base vector floor
      if (t.floor) {
        mapCtx.fillStyle = '#1e293b';
        mapCtx.fillRect(dx, dy, baseTileSize, baseTileSize);
        mapCtx.strokeStyle = 'rgba(51, 65, 85, 0.3)';
        mapCtx.strokeRect(dx, dy, baseTileSize, baseTileSize);
      }

      // Base vector wall
      if (t.wall) {
        mapCtx.fillStyle = '#475569';
        mapCtx.fillRect(dx, dy, baseTileSize, baseTileSize);
        mapCtx.strokeStyle = '#334155';
        mapCtx.strokeRect(dx, dy, baseTileSize, baseTileSize);
      }

      // Base vector spill
      if (t.spill) {
        const s = t.spill.toLowerCase();
        mapCtx.fillStyle = s.includes('lava') ? '#dc2626' : s.includes('acid') ? '#16a34a' : '#2563eb';
        mapCtx.fillRect(dx, dy, baseTileSize, baseTileSize);
      }

      // Base vector trap
      if (showTraps && t.trap) {
        const tr = t.trap.toLowerCase();
        mapCtx.fillStyle = tr.includes('pit') || tr.includes('hole') ? '#1c1917' : 'rgba(239, 68, 68, 0.4)';
        mapCtx.fillRect(dx, dy, baseTileSize, baseTileSize);
      }

      // Base vector boulder
      if (showBoulders && t.boulder) {
        mapCtx.fillStyle = '#78716c';
        mapCtx.beginPath();
        mapCtx.arc(dx + baseTileSize / 2, dy + baseTileSize / 2, baseTileSize * 0.4, 0, Math.PI * 2);
        mapCtx.fill();
        mapCtx.strokeStyle = '#d6d3d1';
        mapCtx.lineWidth = 1.5;
        mapCtx.stroke();
      }

      // Base vector fixture
      if (showFixtures && t.fixture) {
        mapCtx.fillStyle = '#0284c7';
        mapCtx.fillRect(dx + 4, dy + 4, baseTileSize - 8, baseTileSize - 8);
      }

      // Base vector door
      if (t.door) {
        mapCtx.fillStyle = t.doorState === 'Open' ? '#d97706' : '#92400e';
        mapCtx.fillRect(dx, dy, baseTileSize, baseTileSize);
      }

      // Base vector passage
      if (t.passage) {
        const p = t.passage.toLowerCase();
        mapCtx.fillStyle = p.includes('down') ? '#059669' : p.includes('up') ? '#d97706' : '#7c3aed';
        mapCtx.fillRect(dx, dy, baseTileSize, baseTileSize);
      }

      // Official Game Textures (if enabled)
      if (showTextures) {
        // 1. Floor ground
        if (t.floorGlyph) {
          const floorImg = getTileImage(t.floorGlyph);
          if (floorImg) mapCtx.drawImage(floorImg, dx, dy, baseTileSize, baseTileSize);
        }
        // 2. Spill
        if (t.spillGlyph) {
          const spillImg = getTileImage(t.spillGlyph);
          if (spillImg) mapCtx.drawImage(spillImg, dx, dy, baseTileSize, baseTileSize);
        }
        // 3. Trap (drawn directly on ground)
        if (showTraps && t.trapGlyph) {
          const trapImg = getTileImage(t.trapGlyph);
          if (trapImg) mapCtx.drawImage(trapImg, dx, dy, baseTileSize, baseTileSize);
        }
        // 4. Fixture (controlled by showFixtures)
        if (showFixtures && t.fixtureGlyph) {
          const fixtureImg = getTileImage(t.fixtureGlyph);
          if (fixtureImg) mapCtx.drawImage(fixtureImg, dx, dy, baseTileSize, baseTileSize);

          // Highlight Blessed / Cursed fixtures directly on map (e.g. fountains, altars)
          if (t.fixtureSanctity === 'blessed' || t.fixtureSanctity === 'cursed') {
            const isBlessed = t.fixtureSanctity === 'blessed';
            mapCtx.save();
            mapCtx.strokeStyle = isBlessed ? '#10b981' : '#f43f5e';
            mapCtx.lineWidth = 1.6;
            mapCtx.strokeRect(dx + 1.5, dy + 1.5, baseTileSize - 3, baseTileSize - 3);
            // Tiny top-left B/C badge dot
            const dotX = dx + 5;
            const dotY = dy + 5;
            mapCtx.beginPath();
            mapCtx.arc(dotX, dotY, 3.2, 0, Math.PI * 2);
            mapCtx.fillStyle = isBlessed ? '#10b981' : '#f43f5e';
            mapCtx.fill();
            mapCtx.strokeStyle = '#0a0d14';
            mapCtx.lineWidth = 1;
            mapCtx.stroke();
            mapCtx.restore();
          }
        }
        // 5. Door
        if (t.doorGlyph) {
          const doorImg = getTileImage(t.doorGlyph);
          if (doorImg) mapCtx.drawImage(doorImg, dx, dy, baseTileSize, baseTileSize);
        }
        // 6. Passage / Stairs
        if (t.passageGlyph) {
          const passageImg = getTileImage(t.passageGlyph);
          if (passageImg) mapCtx.drawImage(passageImg, dx, dy, baseTileSize, baseTileSize);
        }
        // 7. Boulder (sits on top of square)
        if (showBoulders && t.boulderGlyph) {
          const bImg = getTileImage(t.boulderGlyph);
          if (bImg) mapCtx.drawImage(bImg, dx, dy, baseTileSize, baseTileSize);
        }
        // 8. Wall
        if (t.wallGlyph) {
          const wallImg = getTileImage(t.wallGlyph);
          if (wallImg) mapCtx.drawImage(wallImg, dx, dy, baseTileSize, baseTileSize);
        }
      }



      // Highlighting: Stairs & Portals
      if (showStairs && t.passage) {
        const p = t.passage.toLowerCase();
        const isDown = p.includes('down');
        const isUp = p.includes('up');
        mapCtx.strokeStyle = isDown ? '#10b981' : isUp ? '#fbbf24' : '#c084fc';
        mapCtx.lineWidth = 2.5;
        mapCtx.strokeRect(dx + 1, dy + 1, baseTileSize - 2, baseTileSize - 2);

        mapCtx.fillStyle = isDown ? '#10b981' : isUp ? '#fbbf24' : '#c084fc';
        mapCtx.font = 'bold 12px sans-serif';
        mapCtx.textAlign = 'center';
        mapCtx.textBaseline = 'middle';
        mapCtx.fillText(isDown ? '▼' : isUp ? '▲' : '★', dx + baseTileSize / 2, dy + baseTileSize / 2);
      }

      // Highlighting: Secret Doors
      if (showSecrets && t.isSecretDoor) {
        mapCtx.strokeStyle = '#c084fc';
        mapCtx.lineWidth = 2;
        mapCtx.setLineDash([3, 3]);
        mapCtx.strokeRect(dx + 1, dy + 1, baseTileSize - 2, baseTileSize - 2);
        mapCtx.setLineDash([]);
      }
    }

    // 3. Draw Shops, Merchants and Special Facilities (if enabled)
    if (showShops && currentFloorData.shops && currentFloorData.shops.length > 0) {
      for (let i = 0; i < currentFloorData.shops.length; i++) {
        const sh = currentFloorData.shops[i];
        const sx = sh.x * baseTileSize;
        const sy = sh.y * baseTileSize;
        const cx = sx + baseTileSize / 2;
        const cy = sy + baseTileSize / 2;

        mapCtx.save();

        const isFacility = sh.isShrineOrStation;
        const mainColor = isFacility ? '#22d3ee' : '#fbbf24';
        const glowColor = isFacility ? 'rgba(6, 182, 212, 0.25)' : 'rgba(245, 158, 11, 0.25)';
        const radius = baseTileSize * 0.44;

        // Subtle ambient glow on tile
        mapCtx.fillStyle = glowColor;
        mapCtx.fillRect(sx, sy, baseTileSize, baseTileSize);

        // Circular black background padding with crisp border
        mapCtx.beginPath();
        mapCtx.arc(cx, cy, radius, 0, Math.PI * 2);
        mapCtx.fillStyle = '#0a0d14';
        mapCtx.fill();
        mapCtx.strokeStyle = mainColor;
        mapCtx.lineWidth = 1.6;
        mapCtx.stroke();

        // Icon texture from tileset, scaled down to fit nicely within circular padding
        if (sh.glyph) {
          const shopImg = getTileImage(sh.glyph);
          if (shopImg) {
            const iconScale = 0.68;
            const iconSize = baseTileSize * iconScale;
            mapCtx.drawImage(shopImg, cx - iconSize / 2, cy - iconSize / 2, iconSize, iconSize);
          }
        }

        // Tiny indicator dot at top-right of the circle
        const dotAngle = -Math.PI / 4;
        const dotX = cx + radius * Math.cos(dotAngle);
        const dotY = cy + radius * Math.sin(dotAngle);
        mapCtx.beginPath();
        mapCtx.arc(dotX, dotY, 2.8, 0, Math.PI * 2);
        mapCtx.fillStyle = mainColor;
        mapCtx.fill();
        mapCtx.strokeStyle = '#0a0d14';
        mapCtx.lineWidth = 1;
        mapCtx.stroke();

        mapCtx.restore();
      }
    }

    // 3.5. Draw Ground, Covered & Monster Items (if enabled)
    if (showItems && currentItemMap && currentItemMap.size > 0) {
      currentItemMap.forEach((tileItems, key) => {
        if (!tileItems || tileItems.length === 0) return;
        const [tx, ty] = key.split(',').map(Number);
        const dx = tx * baseTileSize;
        const dy = ty * baseTileSize;
        const cx = dx + baseTileSize / 2;
        const cy = dy + baseTileSize / 2;

        let hasCarried = tileItems.some(it => it.state === 'carried');
        let hasCovered = tileItems.some(it => it.state === 'covered');
        let hasArtifact = tileItems.some(it => it.isArtifact);

        let primaryItem = tileItems.find(it => it.isArtifact) ||
                          tileItems.find(it => it.state === 'carried') ||
                          tileItems.find(it => it.state === 'covered') ||
                          tileItems[0];

        mapCtx.save();

        // Dark circular backing for contrast
        const radius = baseTileSize * 0.42;
        mapCtx.beginPath();
        mapCtx.arc(cx, cy, radius, 0, Math.PI * 2);
        mapCtx.fillStyle = 'rgba(10, 13, 20, 0.72)';
        mapCtx.fill();

        // Special outlines per user requirements:
        // - Covered / 被压住: BLUE (蓝色轮廓)
        // - Carried on monster / 怪物身上: RED (红色轮廓)
        // - Artifact: Gold glow
        if (hasCarried) {
          mapCtx.strokeStyle = '#ef4444'; // Bright Red
          mapCtx.lineWidth = 2.4;
          mapCtx.shadowColor = 'rgba(239, 68, 68, 0.8)';
          mapCtx.shadowBlur = 4;
          mapCtx.stroke();
          mapCtx.fillStyle = 'rgba(239, 68, 68, 0.12)';
          mapCtx.fill();
        } else if (hasCovered) {
          mapCtx.strokeStyle = '#38bdf8'; // Sky/Electric Blue
          mapCtx.lineWidth = 2.4;
          mapCtx.shadowColor = 'rgba(56, 189, 248, 0.8)';
          mapCtx.shadowBlur = 4;
          mapCtx.stroke();
          mapCtx.fillStyle = 'rgba(56, 189, 248, 0.12)';
          mapCtx.fill();
        } else if (hasArtifact) {
          mapCtx.strokeStyle = '#fbbf24'; // Gold
          mapCtx.lineWidth = 2.0;
          mapCtx.shadowColor = 'rgba(251, 191, 36, 0.8)';
          mapCtx.shadowBlur = 4;
          mapCtx.stroke();
        } else {
          mapCtx.strokeStyle = 'rgba(148, 163, 184, 0.45)';
          mapCtx.lineWidth = 1.0;
          mapCtx.stroke();
        }

        mapCtx.shadowBlur = 0;

        // Draw item texture from tileset
        if (primaryItem && primaryItem.glyph) {
          const itemImg = getTileImage(primaryItem.glyph);
          if (itemImg) {
            const iconSize = baseTileSize * 0.70;
            mapCtx.drawImage(itemImg, cx - iconSize / 2, cy - iconSize / 2, iconSize, iconSize);
          }
        }

        // Multi-item stack count badge (e.g. "×3")
        if (tileItems.length > 1) {
          const badgeX = dx + baseTileSize - 6.5;
          const badgeY = dy + baseTileSize - 6.5;
          mapCtx.beginPath();
          mapCtx.arc(badgeX, badgeY, 6.5, 0, Math.PI * 2);
          mapCtx.fillStyle = '#0a0d14';
          mapCtx.fill();
          mapCtx.strokeStyle = hasCarried ? '#ef4444' : hasCovered ? '#38bdf8' : '#e2e8f0';
          mapCtx.lineWidth = 1.2;
          mapCtx.stroke();

          mapCtx.fillStyle = '#f8fafc';
          mapCtx.font = 'bold 8px sans-serif';
          mapCtx.textAlign = 'center';
          mapCtx.textBaseline = 'middle';
          mapCtx.fillText(`${tileItems.length}`, badgeX, badgeY);
        }

        mapCtx.restore();
      });
    }

    // 4. Hover Glow & Zone Highlight
    if (hoveredTile) {
      mapCtx.save();
      mapCtx.strokeStyle = '#f59e0b';
      mapCtx.lineWidth = 2.5;
      mapCtx.strokeRect(hoveredTile.x * baseTileSize, hoveredTile.y * baseTileSize, baseTileSize, baseTileSize);

      // If hovering over a shop, accentuate the circular badge and draw subtle dashed room outline
      const shopHit = currentShopMap.get(`${hoveredTile.x},${hoveredTile.y}`);
      if (shopHit) {
        const hcx = hoveredTile.x * baseTileSize + baseTileSize / 2;
        const hcy = hoveredTile.y * baseTileSize + baseTileSize / 2;
        const hRadius = baseTileSize * 0.48;
        mapCtx.strokeStyle = '#ffffff';
        mapCtx.lineWidth = 2;
        mapCtx.beginPath();
        mapCtx.arc(hcx, hcy, hRadius, 0, Math.PI * 2);
        mapCtx.stroke();

        if (shopHit.zoneLeft !== null && shopHit.zoneRight !== null) {
          const zx = shopHit.zoneLeft * baseTileSize;
          const zy = shopHit.zoneTop * baseTileSize;
          const zw = (shopHit.zoneRight - shopHit.zoneLeft + 1) * baseTileSize;
          const zh = (shopHit.zoneBottom - shopHit.zoneTop + 1) * baseTileSize;

          mapCtx.strokeStyle = shopHit.isShrineOrStation ? 'rgba(34, 211, 238, 0.7)' : 'rgba(251, 191, 36, 0.7)';
          mapCtx.lineWidth = 1.5;
          mapCtx.setLineDash([4, 4]);
          mapCtx.strokeRect(zx, zy, zw, zh);
          mapCtx.fillStyle = shopHit.isShrineOrStation ? 'rgba(34, 211, 238, 0.06)' : 'rgba(251, 191, 36, 0.06)';
          mapCtx.fillRect(zx, zy, zw, zh);
        }
      }
      mapCtx.restore();
    }

    mapCtx.restore();
  }

  function updateTileTooltip(tileX, tileY, mouseX, mouseY) {
    if (!mapCanvasContainer) return;
    const rect = mapCanvasContainer.getBoundingClientRect();
    const hit = currentTileMap.get(`${tileX},${tileY}`);
    const shopHit = currentShopMap.get(`${tileX},${tileY}`);
    const itemHit = (showItems && currentItemMap) ? currentItemMap.get(`${tileX},${tileY}`) : null;

    if (hit || shopHit || itemHit) {
      const currentHit = hit || { x: tileX, y: tileY };
      if (!hoveredTile || hoveredTile.x !== currentHit.x || hoveredTile.y !== currentHit.y) {
        hoveredTile = currentHit;
        renderMapCanvas();
      }
      if (mapTooltip) {
        const isEn = !I18N.isZh();
        const renderBucBadge = (sanctity) => {
          if (sanctity === 'blessed') {
            return `<span class="px-1 py-0.5 rounded text-[10px] font-bold bg-emerald-950/90 text-emerald-300 border border-emerald-700/80 shrink-0">${isEn ? 'Blessed' : '祝福'}</span>`;
          }
          if (sanctity === 'cursed') {
            return `<span class="px-1 py-0.5 rounded text-[10px] font-bold bg-rose-950/90 text-rose-400 border border-rose-700/80 shrink-0">${isEn ? 'Cursed' : '诅咒'}</span>`;
          }
          if (sanctity === 'uncursed') {
            return `<span class="px-1 py-0.5 rounded text-[10px] font-medium bg-surface-800/90 text-slate-400 border border-surface-700/70 shrink-0">${isEn ? 'Uncursed' : '未诅咒'}</span>`;
          }
          return '';
        };
        let tipHtml = '';
        if (shopHit) {
          const badgeColor = shopHit.isShrineOrStation ? 'text-cyan-400' : 'text-amber-400';
          const genderStr = isEn
            ? (shopHit.merchantGender === 'female' ? ' (Female)' : shopHit.merchantGender === 'male' ? ' (Male)' : '')
            : (shopHit.merchantGender === 'female' ? ' (女)' : shopHit.merchantGender === 'male' ? ' (男)' : '');
          const keeperLabel = isEn ? 'Keeper:' : '店主:';
          const keeperHtml = shopHit.merchantName 
            ? `<span class="text-slate-300 ml-2">${keeperLabel} <strong class="text-amber-200">${shopHit.merchantName}</strong>${genderStr}</span>`
            : '';
          const shopTypeName = isEn ? (shopHit.shopType || shopHit.displayName) : (shopHit.displayName || shopHit.shopType);
          tipHtml += `<div class="mb-1 pb-1 border-b border-surface-700/80 font-bold ${badgeColor} flex items-center justify-between">
            <span>${shopHit.isShrineOrStation ? '✨' : '🏪'} ${shopTypeName}</span>
            ${keeperHtml}
          </div>`;
        }
        if (hit && hit.fixture && hit.fixtureSanctity) {
          const fixName = isEn ? hit.fixture : (hit.fixtureChinese || hit.fixture);
          tipHtml += `<div class="mb-1 pb-1 border-b border-surface-700/60 flex items-center gap-1.5 text-xs font-semibold text-cyan-200">
            ${renderBucBadge(hit.fixtureSanctity)}
            <span>✨ ${fixName}</span>
          </div>`;
        }
        if (hit) {
          tipHtml += `<div class="text-slate-300">${hit.tooltip || (isEn ? `Coords: (${tileX}, ${tileY})` : `坐标: (${tileX}, ${tileY})`)}</div>`;
        } else if (shopHit) {
          tipHtml += `<div class="text-slate-400 text-[11px]">${isEn ? `Coords: (${shopHit.globalX}, ${shopHit.globalY})` : `坐标: (${shopHit.globalX}, ${shopHit.globalY})`}</div>`;
        }

        if (itemHit && itemHit.length > 0) {
          const itemsHeader = isEn ? `📦 Items (${itemHit.length})` : `📦 道具清单 (${itemHit.length}件)`;
          tipHtml += `
            <div class="mt-2 pt-2 border-t border-surface-700/80">
              <div class="text-[11px] font-bold text-amber-300 flex items-center justify-between mb-1.5">
                <span>${itemsHeader}</span>
              </div>
              <div class="space-y-1 max-h-[140px] overflow-y-auto pr-0.5">
                ${itemHit.map(it => {
                  const carrierText = isEn ? (it.carrierName || it.carrierChinese) : (it.carrierChinese || it.carrierName);
                  const boulderText = isEn ? (it.boulderName || it.boulderChinese) : (it.boulderChinese || it.boulderName);
                  const containerText = isEn ? (it.containerName || it.containerChinese) : (it.containerChinese || it.containerName);
                  const stateBadge = it.state === 'carried' 
                    ? `<span class="px-1.5 py-0.5 rounded text-[10px] font-bold bg-red-950 text-red-400 border border-red-800">${isEn ? `👾 Carried (${carrierText})` : `👾 怪物携带 (${carrierText})`}</span>`
                    : it.state === 'covered'
                    ? `<span class="px-1.5 py-0.5 rounded text-[10px] font-bold bg-blue-950 text-sky-400 border border-blue-800">${isEn ? `🪨 Under Boulder (${boulderText})` : `🪨 被压住 (${boulderText})`}</span>`
                    : it.state === 'container'
                    ? `<span class="px-1.5 py-0.5 rounded text-[10px] font-bold bg-amber-950 text-amber-400 border border-amber-800">${isEn ? `📦 Container (${containerText})` : `📦 容器 (${containerText})`}</span>`
                    : `<span class="px-1.5 py-0.5 rounded text-[10px] font-medium bg-surface-800 text-slate-300">${isEn ? 'Ground' : '地面'}</span>`;
                  const bucBadge = renderBucBadge(it.sanctity);
                  const itName = isEn ? (it.name || it.nameChinese) : (it.nameChinese || it.name);
                  return `
                    <div class="flex items-center justify-between gap-2 text-xs py-0.5 border-b border-surface-800/40 last:border-none">
                      <span class="flex items-center gap-1.5 min-w-0">
                        ${bucBadge}
                        <span class="${it.isArtifact ? 'text-amber-400 font-bold' : 'text-slate-200'} truncate">
                          ${it.isArtifact ? '✨ ' : ''}${itName}${it.count > 1 ? ` ×${it.count}` : ''}
                        </span>
                      </span>
                      <span class="shrink-0">${stateBadge}</span>
                    </div>
                  `;
                }).join('')}
              </div>
            </div>
          `;
        }

        mapTooltip.innerHTML = tipHtml;
        const tipWidth = (itemHit && itemHit.length > 0) ? 370 : 280;
        const posX = (mouseX + 16 + tipWidth > rect.width) ? (mouseX - tipWidth - 10) : (mouseX + 16);
        const posY = Math.min(mouseY + 16, Math.max(10, rect.height - 45));
        mapTooltip.style.left = `${posX}px`;
        mapTooltip.style.top = `${posY}px`;
        mapTooltip.classList.remove('hidden');
      }
    } else {
      if (hoveredTile !== null) {
        hoveredTile = null;
        renderMapCanvas();
      }
      if (mapTooltip) mapTooltip.classList.add('hidden');
    }
  }

  if (mapCanvas && mapCanvasContainer) {
    mapCanvas.addEventListener('mousedown', (e) => {
      isMapDragging = true;
      dragLastX = e.clientX;
      dragLastY = e.clientY;
      mapCanvas.classList.remove('cursor-grab');
      mapCanvas.classList.add('cursor-grabbing');
    });

    window.addEventListener('mouseup', () => {
      isMapDragging = false;
      if (mapCanvas) {
        mapCanvas.classList.remove('cursor-grabbing');
        mapCanvas.classList.add('cursor-grab');
      }
    });

    mapCanvas.addEventListener('mousemove', (e) => {
      const rect = mapCanvasContainer.getBoundingClientRect();
      const mouseX = e.clientX - rect.left;
      const mouseY = e.clientY - rect.top;

      if (isMapDragging) {
        mapOffsetX += e.clientX - dragLastX;
        mapOffsetY += e.clientY - dragLastY;
        dragLastX = e.clientX;
        dragLastY = e.clientY;
        if (mapTooltip) mapTooltip.classList.add('hidden');
        renderMapCanvas();
        return;
      }

      const tileX = Math.floor((mouseX - mapOffsetX) / (baseTileSize * mapScale));
      const tileY = Math.floor((mouseY - mapOffsetY) / (baseTileSize * mapScale));

      if (mapCoordsBadge && currentFloorData) {
        const actualX = tileX + currentFloorData.left;
        const actualY = tileY + currentFloorData.top;
        mapCoordsBadge.textContent = `${I18N.isZh() ? '指针' : 'Cursor'}: (${actualX}, ${actualY})`;
      }

      updateTileTooltip(tileX, tileY, mouseX, mouseY);
    });

    mapCanvas.addEventListener('mouseleave', () => {
      if (hoveredTile !== null) {
        hoveredTile = null;
        renderMapCanvas();
      }
      if (mapTooltip) mapTooltip.classList.add('hidden');
    });

    mapCanvas.addEventListener('wheel', (e) => {
      e.preventDefault();
      const rect = mapCanvasContainer.getBoundingClientRect();
      const mouseX = e.clientX - rect.left;
      const mouseY = e.clientY - rect.top;

      const zoomFactor = e.deltaY < 0 ? 1.15 : 0.85;
      const newScale = Math.min(Math.max(mapScale * zoomFactor, 0.25), 5.0);

      mapOffsetX = mouseX - (mouseX - mapOffsetX) * (newScale / mapScale);
      mapOffsetY = mouseY - (mouseY - mapOffsetY) * (newScale / mapScale);
      mapScale = newScale;

      if (mapZoomLabel) mapZoomLabel.textContent = `${Math.round(mapScale * 100)}%`;
      renderMapCanvas();
    }, { passive: false });
  }

  // Zoom Buttons
  const btnZoomIn = document.getElementById('btn-zoom-in');
  const btnZoomOut = document.getElementById('btn-zoom-out');
  const btnZoomReset = document.getElementById('btn-zoom-reset');

  if (btnZoomIn) {
    btnZoomIn.addEventListener('click', () => {
      mapScale = Math.min(mapScale * 1.25, 5.0);
      if (mapZoomLabel) mapZoomLabel.textContent = `${Math.round(mapScale * 100)}%`;
      renderMapCanvas();
    });
  }

  if (btnZoomOut) {
    btnZoomOut.addEventListener('click', () => {
      mapScale = Math.max(mapScale * 0.8, 0.25);
      if (mapZoomLabel) mapZoomLabel.textContent = `${Math.round(mapScale * 100)}%`;
      renderMapCanvas();
    });
  }

  if (btnZoomReset) {
    btnZoomReset.addEventListener('click', resetMapViewport);
  }

  // Layer Toggles
  function wireLayerToggle(btnId, getState, setState) {
    const btn = document.getElementById(btnId);
    if (!btn) return;
    btn.addEventListener('click', () => {
      const next = !getState();
      setState(next);
      btn.classList.toggle('active', next);
      renderMapCanvas();
    });
  }

  wireLayerToggle('map-toggle-textures', () => showTextures, (v) => showTextures = v);
  wireLayerToggle('map-toggle-items', () => showItems, (v) => showItems = v);
  wireLayerToggle('map-toggle-shops', () => showShops, (v) => showShops = v);
  wireLayerToggle('map-toggle-boulders', () => showBoulders, (v) => showBoulders = v);
  wireLayerToggle('map-toggle-traps', () => showTraps, (v) => showTraps = v);
  wireLayerToggle('map-toggle-stairs', () => showStairs, (v) => showStairs = v);
  wireLayerToggle('map-toggle-fixtures', () => showFixtures, (v) => showFixtures = v);
  wireLayerToggle('map-toggle-secrets', () => showSecrets, (v) => showSecrets = v);

  if (mapTilesetSelect) {
    mapTilesetSelect.addEventListener('change', async (e) => {
      await applyTilesetChange(e.target.value, true);
    });
  }

  if (mapFloorSelect) {
    mapFloorSelect.addEventListener('change', () => {
      if (currentReport) {
        loadFloorMap(currentReport.seedText, mapFloorSelect.value);
      }
    });
  }

  async function loadMapCatalog(seed) {
    try {
      const res = await fetch(`/api/map/catalog?seed=${encodeURIComponent(seed)}`);
      if (!res.ok) return;
      const data = await res.json();
      currentMapCatalog = data.maps || [];

      const groups = {};
      currentMapCatalog.forEach(m => {
        if (!groups[m.group]) groups[m.group] = [];
        groups[m.group].push(m);
      });

      let html = '';
      Object.keys(groups).forEach(gName => {
        html += `<optgroup label="${gName}">`;
        groups[gName].forEach(m => {
          html += `<option value="${m.id}">${m.displayName} (${m.width}×${m.height})</option>`;
        });
        html += `</optgroup>`;
      });

      if (mapFloorSelect) {
        mapFloorSelect.innerHTML = html;
      }

      if (currentMapCatalog.length > 0) {
        loadFloorMap(seed, currentMapCatalog[0].id);
      }
    } catch (err) {
      console.error('Failed to load map catalog:', err);
    }
  }

  async function loadFloorMap(seed, mapId, skipResetViewport = false) {
    if (mapLoadingOverlay) mapLoadingOverlay.classList.remove('hidden');
    try {
      const res = await fetch(`/api/map/data?seed=${encodeURIComponent(seed)}&map=${encodeURIComponent(mapId)}`);
      if (!res.ok) return;
      const data = await res.json();
      currentFloorData = data;

      currentTileMap.clear();
      (data.tiles || []).forEach(t => {
        currentTileMap.set(`${t.x},${t.y}`, t);
        if (t.boulderGlyph) getTileImage(t.boulderGlyph);
        if (t.trapGlyph) getTileImage(t.trapGlyph);
      });

      currentShopMap.clear();
      const shops = data.shops || [];
      shops.forEach(sh => {
        currentShopMap.set(`${sh.x},${sh.y}`, sh);
        if (sh.glyph) {
          getTileImage(sh.glyph);
        }
      });

      currentItemMap.clear();
      const items = data.items || [];
      items.forEach(it => {
        const key = `${it.x},${it.y}`;
        if (!currentItemMap.has(key)) {
          currentItemMap.set(key, []);
        }
        currentItemMap.get(key).push(it);
        if (it.glyph) {
          getTileImage(it.glyph);
        }
      });

      const mapItemsCount = document.getElementById('map-items-count');
      if (mapItemsCount) {
        mapItemsCount.textContent = data.itemsCount ?? items.length;
      }

      const mapShopsCount = document.getElementById('map-shops-count');
      if (mapShopsCount) {
        mapShopsCount.textContent = shops.length;
      }

      const mapBouldersCount = document.getElementById('map-boulders-count');
      if (mapBouldersCount) {
        mapBouldersCount.textContent = data.bouldersCount ?? 0;
      }

      const mapTrapsCount = document.getElementById('map-traps-count');
      if (mapTrapsCount) {
        mapTrapsCount.textContent = data.trapsCount ?? 0;
      }

      const fixturesCount = (data.tiles || []).filter(t => !!t.fixture).length;
      const mapFixturesCount = document.getElementById('map-fixtures-count');
      if (mapFixturesCount) {
        mapFixturesCount.textContent = fixturesCount;
      }

      if (mapSizeBadge) {
        mapSizeBadge.textContent = `${data.width} × ${data.height} ${I18N.t('map.gridUnit')}`;
      }

      resizeMapCanvas();
      if (!skipResetViewport) {
        resetMapViewport();
      }
    } catch (err) {
      console.error('Failed to load floor map:', err);
    } finally {
      if (mapLoadingOverlay) mapLoadingOverlay.classList.add('hidden');
    }
  }

  async function jumpToArtifactMap(rawName, site, levelIndex, gx, gy, artName) {
    if (!currentReport) return;

    // 1. Ensure catalog is loaded
    if (!currentMapCatalog || currentMapCatalog.length === 0) {
      await loadMapCatalog(currentReport.seedText);
    }

    // 2. Locate target map
    let targetMap = null;
    if (rawName && currentMapCatalog && currentMapCatalog.length > 0) {
      targetMap = currentMapCatalog.find(m => m.rawName === rawName);
    }
    if (!targetMap && currentMapCatalog && currentMapCatalog.length > 0) {
      targetMap = currentMapCatalog.find(m => m.site === site && m.levelIndex == levelIndex);
    }
    if (!targetMap && currentMapCatalog && currentMapCatalog.length > 0) {
      targetMap = currentMapCatalog[0];
    }
    if (!targetMap) return;

    // 3. Switch subtab to Map Viewer
    skipNextMapReset = true;
    Object.keys(subtabs).forEach(k => {
      const t = subtabs[k];
      if (t && t.btn && t.content) {
        t.btn.classList.remove('active', 'border-brand-500', 'text-brand-400', 'bg-surface-900');
        t.btn.classList.add('border-transparent', 'text-slate-400');
        t.content.classList.add('hidden');
      }
    });
    if (subtabs.map && subtabs.map.btn && subtabs.map.content) {
      subtabs.map.btn.classList.add('active', 'border-brand-500', 'text-brand-400', 'bg-surface-900');
      subtabs.map.btn.classList.remove('border-transparent', 'text-slate-400');
      subtabs.map.content.classList.remove('hidden');
    }

    // 4. Update dropdown selector
    if (mapFloorSelect) {
      mapFloorSelect.value = targetMap.id;
    }

    // 5. Load floor map if not currently loaded
    if (!currentFloorData || currentFloorData.mapId !== targetMap.id) {
      await loadFloorMap(currentReport.seedText, targetMap.id, true);
    }

    if (!currentFloorData || !mapCanvasContainer) return;

    // 6. Center canvas viewport on target coordinates (or whole floor if coordinates are 0,0)
    resizeMapCanvas();
    if (gx === 0 && gy === 0) {
      resetMapViewport();
    } else {
      const localX = gx - currentFloorData.left;
      const localY = gy - currentFloorData.top;

      const rect = mapCanvasContainer.getBoundingClientRect();
      mapScale = Math.max(mapScale, 1.5);
      const targetPixelX = (localX + 0.5) * baseTileSize;
      const targetPixelY = (localY + 0.5) * baseTileSize;
      mapOffsetX = rect.width / 2 - targetPixelX * mapScale;
      mapOffsetY = rect.height / 2 - targetPixelY * mapScale;

      if (mapZoomLabel) mapZoomLabel.textContent = `${Math.round(mapScale * 100)}%`;

      // 7. Focus tile, render, and show tooltip
      const hit = currentTileMap.get(`${localX},${localY}`);
      hoveredTile = hit || { x: localX, y: localY };
      renderMapCanvas();

      if (mapCoordsBadge) {
        mapCoordsBadge.textContent = `${I18N.isZh() ? '指针' : 'Cursor'}: (${gx}, ${gy})`;
      }

      const centerX = rect.width / 2;
      const centerY = rect.height / 2;
      updateTileTooltip(localX, localY, centerX, centerY);
    }

    // 8. Smooth scroll view to map viewer
    if (subtabs.map && subtabs.map.content) {
      subtabs.map.content.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
  }

  // -------------------------------------------------------------
  // Initial Boot
  // -------------------------------------------------------------
  async function loadSuggestions() {
    try {
      const res = await fetch('/api/suggestions');
      if (res.ok) {
        const data = await res.json();
        const dl = document.getElementById('artifact-datalist');
        if (dl) {
          dl.innerHTML = data.artifacts.map(a => `<option value="${a.cn}">${a.en}</option>`).join('');
        }
      }
    } catch { }
  }

  async function init() {
    // 1. Load settings (auto-detects or reads config.json)
    await loadSettings();

    // 2. Fetch suggestions to fill datalists
    await loadSuggestions();

    // 3. Run initial analyze if configured
    if (isConfiguredState) {
      runAnalyze('test1234');
    }
  }

  init();
});
