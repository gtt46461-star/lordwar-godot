using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NeoModLoader.api;
using NeoModLoader.AndroidCompatibilityModule;
using NeoModLoader.General;
using NeoModLoader.General.UI.Tab;
using UnityEngine;
using UnityEngine.UI;

namespace LordWar.AndroidMod
{
    /// <summary>WorldBox owns the world, actors, cities, time, visuals and save.</summary>
    public sealed class LordWarMod : BasicMod<LordWarMod>
    {
        private const string WindowId = "lordwar_city_window";
        private const string SelectPowerId = "lordwar_select_city";
        private const string HumanAssetId = "human";
        private const string FirstMappedUnitId = "U001";
        private const int FirstMappedUnitGoldCost = 27;
        private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
        private static readonly MainSection[] MainSections =
        {
            new MainSection("start", "领主开局", "realm", "从原版地图选择人类城市；只显示原版城市和人物。",
                "原版地图选城、城市与成年人类居民读取已接入；完整开局规则尚未完成。"),
            new MainSection("construction", "城市建设", "realm", "读取原版城市建筑和在建建筑。",
                "原版建筑只读信息已接入；建设规划、资源扣除和下达建造订单尚未完成。"),
            new MainSection("treasury", "国库军资", "economy", "读取原版城市现有资源。",
                "原版资源只读信息已接入；独立国库、军资预算和收支规则尚未完成。"),
            new MainSection("population", "人口职业", "economy", "读取原版人类居民与原版战士。",
                "成年人类和原版战士只读统计已接入；完整职业分配和人口政策尚未完成。"),
            new MainSection("proposals", "政务审批", "government", "政务请求审批入口。",
                "已接入单一会话内的乡兵申请、同意、拒绝、稍后；多类固定政务箱和申请存档尚未完成。"),
            new MainSection("officials", "官员任免", "government", "从原版成年人类居民中任命原版城主。",
                "原版城主任命和回读确认已接入；完整官职资格、任期和替补规则尚未完成。"),
            new MainSection("generals", "将军与家族", "army", "读取原版军队并任命原版军队长。",
                "原版军队长任命和回读确认已接入；将军履历、家族系统和其他军职尚未完成。"),
            new MainSection("units", "兵种解锁", "army", "查看人类兵种与装备适配进度。",
                "源码适配 U001 乡兵：调用原版征募资格和战士职业，27 金币；3 天训练折叠到批准时完成。其余 155 常规与 120 特殊兵种未映射。"),
            new MainSection("recruitment", "征募训练", "recruitment", "真实居民征募和训练。",
                "源码接入：原版人类居民申请，经批准扣除 27 原版城市金币，再调用公开的原版 City.makeWarrior；训练时长折叠，申请队列暂不持久化。"),
            new MainSection("equipment", "装备成长", "recruitment", "原版人物装备与成长。",
                "尚未完成：没有装备消耗、装备成长、耐久或升级规则接入。"),
            new MainSection("skills", "技能特性", "policies", "技能和特性适配进度。",
                "尚未完成：360 项技能与特性尚未映射到原版特质、装备或战斗触发。"),
            new MainSection("policies", "国策外交军令", "policies", "政策、外交、附庸和军团命令。",
                "尚未完成：政策效果、外交附庸、行军补给与军团命令尚未接入。")
        };
        private ScrollWindow _cityWindow;
        private long _cityId = -1;
        private long _kingdomId = -1;
        private long _actorId = -1;
        private int _activeSection;
        private string _status = "请选择原版城市";
        private long _pendingRecruitCityId = -1;
        private long _pendingRecruitKingdomId = -1;
        private long _pendingRecruitActorId = -1;
        private string _proposalMessage = "当前没有待审批申请";

        protected override void OnModLoad()
        {
            WriteDiagnostic("MANAGED_MOD_ENTERED");
            try
            {
                Sprite cityIcon = null;
                foreach (string iconPath in new[] { "ui/icons/iconCity", "ui/icons/iconSteam" })
                {
                    try
                    {
                        cityIcon = SpriteTextureLoader.getSprite(iconPath);
                        if (cityIcon != null) break;
                    }
                    catch (Exception error)
                    {
                        LogInfo("LordWar icon unavailable " + iconPath + ": " + error.Message);
                    }
                }
                if (cityIcon == null)
                    throw new InvalidOperationException("WorldBox UI icons unavailable; LordWar tab cannot be registered");

                Sprite[] originalBombIcons = CaptureOriginalBombIcons();
                var tab = TabManager.CreateTab("lordwar", "领主战争", "在原版城市中执行领主命令", cityIcon);
                ClearTabTemplateButtons(tab);

                AssetManager.powers.add(new GodPower
                {
                    id = SelectPowerId,
                    name = "领主战争 · 地图选城",
                    force_map_mode = MetaType.City,
                    click_special_action = IL2CPPHelper.C<PowerActionWithID>(
                        (Func<global::WorldTile, string, bool>)SelectCityFromMap),
                    unselect_when_window = true
                });
                RegisterMainEntries(tab, cityIcon, originalBombIcons);
                if (TryReplaceBombCategory(tab, cityIcon))
                {
                    LogInfo("LordWar replaced the native destruction category with 12 grouped entries");
                    WriteDiagnostic("BOMB_CATEGORY_REPLACED_WITH_12_ENTRIES");
                }
                else
                {
                    HideUnboundLordWarTab(tab);
                    LogInfo("LordWar could not resolve the pinned NML registry; the temporary tab was hidden and vanilla bomb powers were left intact");
                    WriteDiagnostic("BOMB_CATEGORY_REPLACEMENT_BLOCKED");
                }
            }
            catch (Exception error)
            {
                LogInfo("LordWar native entry FAILED: " + error);
                throw;
            }
        }

        private sealed class MainSection
        {
            public readonly string Id;
            public readonly string Title;
            public readonly string Group;
            public readonly string Description;
            public readonly string Status;

            public MainSection(string id, string title, string group, string description, string status)
            {
                Id = id;
                Title = title;
                Group = group;
                Description = description;
                Status = status;
            }
        }

        private static Sprite[] CaptureOriginalBombIcons()
        {
            PowersTab nativeBombTab = PowerButtonCreator.GetTab(PowerTabNames.Bombs);
            if (nativeBombTab == null) return new Sprite[0];
            PowerButton[] nativeButtons = nativeBombTab.GetComponentsInChildren<PowerButton>(true);
            var icons = new System.Collections.Generic.List<Sprite>();
            foreach (PowerButton button in nativeButtons)
                if (button != null && button.icon != null && button.icon.sprite != null)
                    icons.Add(button.icon.sprite);
            LogInfo("LordWar captured " + icons.Count + " existing bomb-category button icons");
            return icons.ToArray();
        }

        private static void ClearTabTemplateButtons(PowersTab tab)
        {
            PowerButton[] templateButtons = tab.GetComponentsInChildren<PowerButton>(true);
            foreach (PowerButton button in templateButtons)
                if (button != null) button.gameObject.SetActive(false);
            tab._power_buttons.Clear();
            LogInfo("LordWar hid " + templateButtons.Length + " cloned template buttons before registering its 12 entries");
        }

        private void RegisterMainEntries(PowersTab tab, Sprite fallbackIcon, Sprite[] originalIcons)
        {
            tab.SetLayout(new System.Collections.Generic.List<string>
            {
                "realm", "economy", "government", "army", "recruitment", "policies"
            });

            for (int index = 0; index < MainSections.Length; index++)
            {
                MainSection section = MainSections[index];
                Sprite icon = originalIcons != null && index < originalIcons.Length && originalIcons[index] != null
                    ? originalIcons[index] : fallbackIcon;
                PowerButton button;
                if (index == 0)
                {
                    button = PowerButtonCreator.CreateGodPowerButton(SelectPowerId, icon, tab.transform);
                }
                else
                {
                    int capturedIndex = index;
                    button = PowerButtonCreator.CreateSimpleButton(
                        "lordwar_section_" + section.Id,
                        (Action)(() => OpenSection(capturedIndex)), icon, tab.transform);
                }

                TipButton tip = button.GetComponent<TipButton>();
                if (tip != null)
                {
                    tip.textOnClick = section.Title;
                    tip.textOnClickDescription = section.Description;
                }
                tab.AddPowerButton(section.Group, button);
            }
            tab.UpdateLayout();
        }

        // NML 2.0's public CreateTab API creates a tab entry. The pinned Android
        // loader's TabManager keeps parallel private tab_names/tab_entries lists.
        // We rebind the existing "destruction" entry to our NML tab, remove only
        // the temporary LordWar entry, then reflow the same native toolbar.
        private static bool TryReplaceBombCategory(PowersTab lordWarTab, Sprite icon)
        {
            Type managerType = typeof(TabManager);
            FieldInfo namesField = managerType.GetField("tab_names", PrivateStatic);
            FieldInfo entriesField = managerType.GetField("tab_entries", PrivateStatic);
            IList names = namesField == null ? null : namesField.GetValue(null) as IList;
            IList entries = entriesField == null ? null : entriesField.GetValue(null) as IList;
            if (names == null || entries == null || names.Count != entries.Count) return false;

            int lordWarIndex = FindTabIndex(names, "lordwar");
            int bombsIndex = FindTabIndex(names, PowerTabNames.Bombs);
            if (lordWarIndex < 0 || bombsIndex < 0 || lordWarIndex == bombsIndex) return false;

            Button lordWarEntry = entries[lordWarIndex] as Button;
            Button bombsEntry = entries[bombsIndex] as Button;
            PowersTab bombsTab = PowerButtonCreator.GetTab(PowerTabNames.Bombs);
            if (lordWarEntry == null || bombsEntry == null || bombsTab == null) return false;

            // Stage references before changing the UI so an unknown NML layout
            // leaves the original category and its actions untouched.
            Image iconImage = bombsEntry.transform.Find("Icon") == null
                ? null : bombsEntry.transform.Find("Icon").GetComponent<Image>();
            TipButton tip = bombsEntry.GetComponent<TipButton>();
            MethodInfo reflow = managerType.GetMethod("_updateTabLayout", PrivateStatic);
            if (iconImage == null || tip == null || reflow == null) return false;

            Button.ButtonClickedEvent previousClick = bombsEntry.onClick;
            string previousTitle = tip.textOnClick;
            string previousDescription = tip.textOnClickDescription;
            Sprite previousIcon = iconImage.sprite;
            bool previousLordWarEntryActive = lordWarEntry.gameObject.activeSelf;
            var nativeBombButtons = bombsTab.GetComponentsInChildren<PowerButton>(true);
            var nativeBombButtonActiveStates = new bool[nativeBombButtons.Length];
            for (int index = 0; index < nativeBombButtons.Length; index++)
                nativeBombButtonActiveStates[index] = nativeBombButtons[index] != null &&
                    nativeBombButtons[index].gameObject.activeSelf;
            var previousNativeButtonList = new List<PowerButton>();
            foreach (PowerButton oldButton in bombsTab._power_buttons)
                previousNativeButtonList.Add(oldButton);
            var previousNames = new ArrayList(names);
            var previousEntries = new ArrayList(entries);
            ButtonSfx sfx = bombsEntry.GetComponent<ButtonSfx>();
            try
            {
                bombsEntry.onClick = new Button.ButtonClickedEvent();
                bombsEntry.onClick.AddListener(() => lordWarTab.showTab(bombsEntry));
                if (sfx != null) bombsEntry.onClick.AddListener(() => sfx.playSound());
                tip.textOnClick = "领主战争";
                tip.textOnClickDescription = "在此分类中运行领主战争";
                iconImage.sprite = icon;

                foreach (PowerButton oldButton in nativeBombButtons)
                    if (oldButton != null) oldButton.gameObject.SetActive(false);
                // Keep the list pair aligned with the visible original entry order.
                names[bombsIndex] = "lordwar";
                names.RemoveAt(lordWarIndex);
                entries.RemoveAt(lordWarIndex);
                lordWarEntry.gameObject.SetActive(false);
                reflow.Invoke(null, null);
                bombsTab._power_buttons.Clear();
                return true;
            }
            catch (Exception error)
            {
                // Restore the registry pair and native powers if Unity or the
                // pinned private NML layout hook fails during the switch.
                names.Clear();
                foreach (object name in previousNames) names.Add(name);
                entries.Clear();
                foreach (object entry in previousEntries) entries.Add(entry);
                bombsEntry.onClick = previousClick;
                tip.textOnClick = previousTitle;
                tip.textOnClickDescription = previousDescription;
                iconImage.sprite = previousIcon;
                lordWarEntry.gameObject.SetActive(previousLordWarEntryActive);
                for (int index = 0; index < nativeBombButtons.Length; index++)
                    if (nativeBombButtons[index] != null)
                        nativeBombButtons[index].gameObject.SetActive(nativeBombButtonActiveStates[index]);
                bombsTab._power_buttons.Clear();
                foreach (PowerButton oldButton in previousNativeButtonList)
                    bombsTab._power_buttons.Add(oldButton);
                try { reflow.Invoke(null, null); }
                catch (Exception restoreError) { LogInfo("LordWar category layout rollback warning: " + restoreError.Message); }
                LogInfo("LordWar bomb-category mutation rolled back: " + error);
                return false;
            }
        }

        private static int FindTabIndex(IList names, string tabName)
        {
            for (int index = 0; index < names.Count; index++)
                if (string.Equals(names[index] as string, tabName, StringComparison.Ordinal)) return index;
            return -1;
        }

        private static bool IsHumanKingdom(global::Kingdom kingdom)
        {
            if (kingdom == null) return false;
            global::ActorAsset founderSpecies = kingdom.getFounderSpecies();
            return founderSpecies != null && string.Equals(
                founderSpecies.id, HumanAssetId, StringComparison.Ordinal);
        }

        private static void HideUnboundLordWarTab(PowersTab lordWarTab)
        {
            Type managerType = typeof(TabManager);
            FieldInfo namesField = managerType.GetField("tab_names", PrivateStatic);
            FieldInfo entriesField = managerType.GetField("tab_entries", PrivateStatic);
            IList names = namesField == null ? null : namesField.GetValue(null) as IList;
            IList entries = entriesField == null ? null : entriesField.GetValue(null) as IList;
            int index = names == null ? -1 : FindTabIndex(names, "lordwar");
            if (index >= 0 && entries != null && index < entries.Count)
            {
                Button entry = entries[index] as Button;
                if (entry != null) entry.gameObject.SetActive(false);
                entries.RemoveAt(index);
                names.RemoveAt(index);
                MethodInfo reflow = managerType.GetMethod("_updateTabLayout", PrivateStatic);
                if (reflow != null) reflow.Invoke(null, null);
            }
            if (lordWarTab != null) lordWarTab.gameObject.SetActive(false);
        }

        private void OpenSection(int sectionIndex)
        {
            _activeSection = Math.Max(0, Math.Min(sectionIndex, MainSections.Length - 1));
            BindCity(SelectedMetas.selected_city);
            ShowCityWindow();
        }

        // This only runs after the native bootstrap and NML have loaded this
        // mod. The loader's own Latest-Bootstrap.log covers earlier failures.
        // It exports our build/runtime facts, never the game's proprietary code.
        private void WriteDiagnostic(string phase)
        {
            string message = "LordWarMod 0.3.0\nphase=" + phase +
                "\nutc=" + DateTime.UtcNow.ToString("o") +
                "\npackage=" + Application.identifier +
                "\nunity=" + Application.unityVersion +
                "\npersistentDataPath=" + Application.persistentDataPath + "\n";
            string[] roots = {
                Path.Combine(Application.persistentDataPath, "LordWar"),
                "/storage/emulated/0/MelonLoader/com.mkarpenko.worldbox/LordWar"
            };
            foreach (string root in roots)
            {
                try
                {
                    Directory.CreateDirectory(root);
                    File.WriteAllText(Path.Combine(root, "startup-diagnostic.txt"), message);
                    LogInfo("LordWar diagnostic written: " + root);
                }
                catch (Exception error)
                {
                    LogInfo("LordWar diagnostic path unavailable: " + root + ": " + error.Message);
                }
            }
        }

        private bool SelectCityFromMap(global::WorldTile tile, string powerId)
        {
            global::City city = tile == null ? null : tile.zone_city;
            if (!BindCity(city)) return false;
            ShowCityWindow();
            return true;
        }

        private bool BindCity(global::City city)
        {
            if (city == null || city.isRekt() || !IsHumanKingdom(city.kingdom))
            {
                _cityId = -1;
                _kingdomId = -1;
                _actorId = -1;
                _status = "请在原版地图选择一座仍属于人类国家的城市";
                return false;
            }
            if (_cityId != city.getID() || _kingdomId != city.kingdom.getID())
                _actorId = -1;
            _cityId = city.getID();
            _kingdomId = city.kingdom.getID();
            SelectedMetas.selected_city = city;
            _status = "已选择 " + city.name;
            LogInfo("LordWar selected native city id=" + _cityId + " kingdom=" + _kingdomId);
            return true;
        }

        private global::City ResolveCity()
        {
            MapBox world = MapBox.instance;
            global::City selected = SelectedMetas.selected_city;
            if (world == null || world.cities == null || selected == null || _cityId < 0 ||
                selected.getID() != _cityId) return null;
            world.cities.checkLists();
            for (int index = 0; index < world.cities.list.Count; index++)
            {
                global::City city = world.cities.list[index];
                if (city == null || city.getID() != _cityId || city.isRekt()) continue;
                if (city.kingdom == null || city.kingdom.getID() != _kingdomId ||
                    !IsHumanKingdom(city.kingdom)) return null;
                return city;
            }
            return null;
        }

        private static bool BelongsToCity(global::Actor actor, global::City city)
        {
            if (actor == null || actor.isRekt() || actor.asset == null ||
                !string.Equals(actor.asset.id, HumanAssetId, StringComparison.Ordinal) || !actor.isAdult())
                return false;
            global::City home = actor.getCity();
            return home != null && home.getID() == city.getID();
        }

        private global::Actor ResolveActor(global::City city)
        {
            if (city == null || _actorId < 0) return null;
            foreach (global::Actor actor in city.units)
                if (BelongsToCity(actor, city) && actor.getID() == _actorId)
                    return actor;
            return null;
        }

        private void SelectActor(long actorId)
        {
            _actorId = actorId;
            global::City city = ResolveCity();
            global::Actor actor = ResolveActor(city);
            _status = actor == null ? "人物已死亡、离城或城市易主；请选择其他人物"
                : "已选择原版人物 " + actor.getName();
            RefreshCityWindow();
        }

        private void ShowCityWindow()
        {
            try
            {
                if (_cityWindow == null)
                {
                    _cityWindow = WindowCreator.CreateEmptyWindow(WindowId, "领主战争");
                    Transform content = _cityWindow.transform_content;
                    var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
                    layout.spacing = 6f;
                    layout.padding = new RectOffset(10, 10, 10, 10);
                    layout.childControlWidth = true;
                    layout.childForceExpandWidth = true;
                    layout.childControlHeight = false;
                    layout.childForceExpandHeight = false;
                    var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
                    fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                }
                RefreshCityWindow();
                ScrollWindow.showWindow(WindowId);
            }
            catch (Exception error)
            {
                LogInfo("LordWar city window FAILED: " + error);
                throw;
            }
        }

        private void RefreshCityWindow()
        {
            Transform content = _cityWindow.transform_content;
            for (int index = content.childCount - 1; index >= 0; index--)
                UnityEngine.Object.Destroy(content.GetChild(index).gameObject);

            AddText(content, "领主战争 · 原版城市", 40f);
            MainSection section = MainSections[_activeSection];
            AddText(content, section.Title + " · " + section.Description, 58f);
            if (_activeSection == 4)
            {
                RefreshProposalBox(content);
                return;
            }
            global::City city = ResolveCity();
            if (city == null)
            {
                _status = "选中城市不存在、已易主或当前地图已改变；请重新选城";
                AddText(content, _status + "\n" + section.Status, 92f);
                return;
            }

            global::Kingdom kingdom = city.kingdom;
            AddText(content, "城市：" + city.name + "    国家：" + kingdom.name, 56f);
            AddText(content,
                "原版人口：" + city.getPopulationPeople() +
                "    城市金币：" + city.getResourcesAmount("gold") +
                "    王都：" + (kingdom.capital == city ? "是" : "否"), 56f);

            if (_activeSection == 1)
            {
                AddText(content, "原版已登记建筑数：" + city.countBuildings() +
                    "。当前只读取原版建筑；建设规划、材料扣除和建造订单尚未接入。", 64f);
            }
            else if (_activeSection == 2)
            {
                AddText(content, "原版库存：金币 " + city.getResourcesAmount("gold") +
                    "，食物 " + city.getResourcesAmount("food") +
                    "，木材 " + city.getResourcesAmount("wood") +
                    "，石料 " + city.getResourcesAmount("stone") +
                    "，铁矿 " + city.getResourcesAmount("iron") +
                    "。军资账户和收支结算尚未接入。", 72f);
            }
            else if (_activeSection == 3 || _activeSection == 7 || _activeSection == 8)
            {
                int humanAdults = 0;
                int humanWarriors = 0;
                int otherSpecies = 0;
                foreach (global::Actor resident in city.units)
                {
                    if (resident == null || resident.isRekt()) continue;
                    if (resident.asset == null || !string.Equals(
                        resident.asset.id, HumanAssetId, StringComparison.Ordinal))
                    {
                        otherSpecies++;
                        continue;
                    }
                    if (!resident.isAdult()) continue;
                    humanAdults++;
                    if (resident.isWarrior()) humanWarriors++;
                }
                AddText(content, "原版人类成年居民：" + humanAdults +
                    "，其中原版战士：" + humanWarriors + "；其他种族居民：" + otherSpecies +
                    "（保持原版身份和AI，不进入领主军队）。", 68f);
            }
            else if (_activeSection == 10)
            {
                AddText(content, "适配口径：优先映射原版特质、职业、装备和战斗回调；360 项技能尚未逐项映射或合并。", 62f);
            }
            else if (_activeSection == 11)
            {
                AddText(content, "政务箱、政策效果、外交附庸、军团命令与行军补给都尚未接入原版世界。", 58f);
            }
            else if (_activeSection == 8)
            {
                AddText(content, "征募来源：" + FirstMappedUnitId + " 乡兵。批准时扣原版城市库存金币 " +
                    FirstMappedUnitGoldCost + "，按 WorldBox 原版 checkCanMakeWarrior/makeWarrior 执行；" +
                    "旧表 3 天训练压缩为审批即完成，使用原版武器和装备，不生成额外人物。", 76f);
            }
            AddText(content, "本入口状态：" + section.Status, 64f);

            global::Army army = city.army;
            AddText(content, "原版城主：" + (city.leader == null ? "未任命" : city.leader.getName()) +
                "    军队长：" + (army == null || army.getCaptain() == null
                    ? "没有可任命的原版军队长" : army.getCaptain().getName()), 56f);
            global::Actor selected = ResolveActor(city);
            AddText(content, "当前人物：" + (selected == null ? "未选择" : selected.getName()) +
                "（仅从原版城市居民中选择）", 42f);

            AddText(content, "可任命的人类（优先显示士兵，最多 16 位）：", 35f);
            int shown = 0;
            for (int pass = 0; pass < 2 && shown < 16; pass++)
            {
                foreach (global::Actor actor in city.units)
                {
                    if (!BelongsToCity(actor, city) || actor.isWarrior() != (pass == 0)) continue;
                    long id = actor.getID();
                    AddAction(content, actor.getName() + (actor.isWarrior() ? " · 原版士兵" : " · 居民") +
                        (_actorId == id ? " ✓" : ""), () => SelectActor(id));
                    if (++shown == 16) break;
                }
            }
            if (shown == 0) AddText(content, "城内没有可任命的成年人物", 35f);
            if (_activeSection == 0 || _activeSection == 5)
            {
                AddAction(content, "设为王都", SetCapital);
                AddAction(content, "任命所选人类为城主", AppointCityLeader);
            }
            if (_activeSection == 0 || _activeSection == 6)
                AddAction(content, "任命所选人类士兵为军队长", AppointArmyCaptain);
            if (_activeSection == 8)
                AddAction(content, "申请征募所选人类为乡兵", SubmitRecruitmentRequest);
            AddText(content, _status, 65f);
        }

        private void RefreshProposalBox(Transform content)
        {
            AddText(content, "固定政务箱（当前只含一个会话内的乡兵申请槽）", 52f);
            if (_pendingRecruitActorId < 0)
            {
                AddText(content, "没有待审批的乡兵申请。未批准的申请目前不跨进程/存档保存。", 58f);
                AddText(content, _proposalMessage, 52f);
                return;
            }

            global::City city = ResolveNativeCity(_pendingRecruitCityId, _pendingRecruitKingdomId);
            global::Actor actor = ResolvePendingRecruit(city);
            AddText(content, "申请：将原版人类居民转为 " + FirstMappedUnitId + " 乡兵；" +
                "申请金币 " + FirstMappedUnitGoldCost + "；城市ID " + _pendingRecruitCityId +
                "，人物ID " + _pendingRecruitActorId, 76f);
            if (city == null || actor == null)
            {
                AddText(content, "申请对象已死亡、离城、转属或世界已改变；不能批准。", 58f);
                AddAction(content, "清除失效申请", RejectRecruitmentRequest);
                AddText(content, _proposalMessage, 52f);
                return;
            }

            AddText(content, "当前原版人物：" + actor.getName() +
                "；当前城市：" + city.name + "；审批前不会扣资源或更改职业。", 58f);
            AddAction(content, "同意并按原版规则征募", ApproveRecruitmentRequest);
            AddAction(content, "拒绝申请", RejectRecruitmentRequest);
            AddAction(content, "稍后处理", DeferRecruitmentRequest);
            AddText(content, _proposalMessage, 52f);
        }

        private void SubmitRecruitmentRequest()
        {
            global::City city = ResolveCity();
            global::Actor actor = ResolveActor(city);
            if (_pendingRecruitActorId >= 0)
                _status = "政务箱已有待审批乡兵申请；先处理或拒绝它";
            else if (city == null || actor == null)
                _status = "城市或人物已变化，申请未提交";
            else if (actor.isWarrior() || actor.isKing() ||
                (city.leader != null && city.leader.getID() == actor.getID()))
                _status = "所选人类已是战士、国王或城主，不符合乡兵申请资格";
            else if (!city.checkCanMakeWarrior(actor))
                _status = "WorldBox 原版城市当前不允许将此居民编为战士";
            else
            {
                _pendingRecruitCityId = city.getID();
                _pendingRecruitKingdomId = city.kingdom.getID();
                _pendingRecruitActorId = actor.getID();
                _proposalMessage = "待领主审批；当前不扣金币、不改人物职业";
                _status = "乡兵申请已送入政务箱";
            }
            RefreshCityWindow();
        }

        private global::City ResolveNativeCity(long cityId, long kingdomId)
        {
            MapBox world = MapBox.instance;
            if (world == null || world.cities == null || cityId < 0) return null;
            world.cities.checkLists();
            for (int index = 0; index < world.cities.list.Count; index++)
            {
                global::City city = world.cities.list[index];
                if (city == null || city.isRekt() || city.getID() != cityId ||
                    city.kingdom == null || city.kingdom.getID() != kingdomId ||
                    !IsHumanKingdom(city.kingdom)) continue;
                return city;
            }
            return null;
        }

        private global::Actor ResolvePendingRecruit(global::City city)
        {
            if (city == null || _pendingRecruitActorId < 0) return null;
            foreach (global::Actor actor in city.units)
                if (actor != null && actor.getID() == _pendingRecruitActorId && BelongsToCity(actor, city))
                    return actor;
            return null;
        }

        private void DeferRecruitmentRequest()
        {
            if (_pendingRecruitActorId >= 0)
            {
                _proposalMessage = "已标记稍后；申请仍待审批，不扣资源";
                _status = _proposalMessage;
            }
            RefreshCityWindow();
        }

        private void RejectRecruitmentRequest()
        {
            ClearPendingRecruitment();
            _proposalMessage = "乡兵申请已拒绝或清除；未扣资源、未更改人物";
            _status = _proposalMessage;
            RefreshCityWindow();
        }

        private void ApproveRecruitmentRequest()
        {
            global::City city = ResolveNativeCity(_pendingRecruitCityId, _pendingRecruitKingdomId);
            global::Actor actor = ResolvePendingRecruit(city);
            if (city == null || actor == null)
            {
                _proposalMessage = "审批失败：原版城市或人类居民已死亡、离城或转属";
                _status = _proposalMessage;
                RefreshCityWindow();
                return;
            }
            if (actor.isWarrior() || actor.isKing() ||
                (city.leader != null && city.leader.getID() == actor.getID()) ||
                !city.checkCanMakeWarrior(actor))
            {
                _proposalMessage = "审批失败：WorldBox 原版资格已变化，申请仍保留供稍后处理或拒绝";
                _status = _proposalMessage;
                RefreshCityWindow();
                return;
            }

            int goldBefore = city.getResourcesAmount("gold");
            if (goldBefore < FirstMappedUnitGoldCost)
            {
                _proposalMessage = "审批失败：原版城市库存金币不足；申请仍保留";
                _status = _proposalMessage;
                RefreshCityWindow();
                return;
            }

            try
            {
                city.takeResource("gold", FirstMappedUnitGoldCost);
                int goldAfterPayment = city.getResourcesAmount("gold");
                if (goldAfterPayment != goldBefore - FirstMappedUnitGoldCost)
                {
                    if (goldAfterPayment < goldBefore)
                        city.addResources("gold", goldBefore - goldAfterPayment);
                    _proposalMessage = "审批失败：原版城市没有准确扣除乡兵费用，未发出征募命令";
                    _status = _proposalMessage;
                    RefreshCityWindow();
                    return;
                }

                city.makeWarrior(actor);
                bool becameWarrior = !actor.isRekt() && actor.isWarrior();
                if (becameWarrior)
                {
                    long actorId = actor.getID();
                    global::Army army = actor.army;
                    bool assignedToOriginalArmy = army != null && army.getCity() != null &&
                        army.getCity().getID() == city.getID();
                    ClearPendingRecruitment();
                    _proposalMessage = assignedToOriginalArmy
                        ? "批准完成：原版人类人物ID " + actorId + " 已转为战士并加入原版城市军队"
                        : "原版已将人物ID " + actorId + " 转为战士，但军队归属回读未确认；金币已按实际职业变化保留";
                    _status = _proposalMessage;
                    LogInfo("LordWar native recruitment city=" + city.getID() +
                        " actor=" + actorId + " makeWarrior_called=true" +
                        " warrior=" + becameWarrior + " army_confirmed=" + assignedToOriginalArmy);
                }
                else
                {
                    int goldBeforeRefund = city.getResourcesAmount("gold");
                    city.addResources("gold", FirstMappedUnitGoldCost);
                    int refunded = city.getResourcesAmount("gold") - goldBeforeRefund;
                    _proposalMessage = "征募失败；原版费用返还 " + refunded + "/" +
                        FirstMappedUnitGoldCost + " 金币；申请仍保留";
                    _status = _proposalMessage;
                    LogInfo("LordWar native recruitment failed city=" + city.getID() +
                        " actor=" + actor.getID() + " returned=" + refunded);
                }
            }
            catch (Exception error)
            {
                bool becameWarriorBeforeError = actor != null && !actor.isRekt() && actor.isWarrior();
                if (becameWarriorBeforeError)
                {
                    ClearPendingRecruitment();
                    _proposalMessage = "原版人物已转为战士后发生异常；为防止重复征募，费用不自动回滚，请核对原版城市库存";
                }
                else
                {
                    int goldCurrent = city.getResourcesAmount("gold");
                    int missing = Math.Max(0, goldBefore - goldCurrent);
                    if (missing > 0) city.addResources("gold", missing);
                    _proposalMessage = "原版征募抛出错误；尝试返还 " + missing + " 金币；请检查城市库存";
                }
                _status = _proposalMessage;
                LogInfo("LordWar native recruitment exception city=" + city.getID() +
                    " actor=" + actor.getID() + " error=" + error);
            }
            RefreshCityWindow();
        }

        private void ClearPendingRecruitment()
        {
            _pendingRecruitCityId = -1;
            _pendingRecruitKingdomId = -1;
            _pendingRecruitActorId = -1;
        }

        private void AppointCityLeader()
        {
            global::City city = ResolveCity();
            global::Actor actor = ResolveActor(city);
            if (city == null || actor == null)
                _status = "城市或人物状态已改变，任命未执行";
            else if (actor.isKing())
                _status = "国王不能兼任此处城主，任命未执行";
            else if (city.leader != null && city.leader.getID() == actor.getID())
                _status = actor.getName() + " 已是原版城主，未重复任命";
            else
            {
                try
                {
                    city.setLeader(actor, true);
                    bool applied = city.leader != null && city.leader.getID() == actor.getID();
                    _status = applied ? "原版城市已任命 " + actor.getName() + " 为城主"
                        : "原版城市未确认城主任命";
                    LogInfo("LordWar native leader result city=" + city.getID() +
                        " actor=" + actor.getID() + " success=" + applied);
                }
                catch (Exception error)
                {
                    _status = "城主任命失败：" + error.Message;
                    LogInfo("LordWar native leader FAILED: " + error);
                }
            }
            RefreshCityWindow();
        }

        private void AppointArmyCaptain()
        {
            global::City city = ResolveCity();
            global::Actor actor = ResolveActor(city);
            if (city == null || actor == null)
                _status = "城市或人物状态已改变，任命未执行";
            else
            {
                global::Army army = city.army;
                global::Army actorArmy = actor.army;
                if (army == null || army.getCity() == null || army.getCity().getID() != city.getID())
                    _status = "此城没有有效的原版军队，任命未执行";
                else if (!actor.isWarrior() || actorArmy == null || actorArmy.getID() != army.getID())
                    _status = "所选人物不是这支原版军队的士兵，任命未执行";
                else if (army.getCaptain() != null && army.getCaptain().getID() == actor.getID())
                    _status = actor.getName() + " 已是军队长，未重复任命";
                else
                {
                    try
                    {
                        army.setCaptain(actor, false);
                        bool applied = army.getCaptain() != null && army.getCaptain().getID() == actor.getID();
                        _status = applied ? "原版军队已任命 " + actor.getName() + " 为军队长"
                            : "原版军队未确认军队长任命";
                        LogInfo("LordWar native captain result army=" + army.getID() +
                            " actor=" + actor.getID() + " success=" + applied);
                    }
                    catch (Exception error)
                    {
                        _status = "军队长任命失败：" + error.Message;
                        LogInfo("LordWar native captain FAILED: " + error);
                    }
                }
            }
            RefreshCityWindow();
        }

        private void SetCapital()
        {
            global::City city = ResolveCity();
            if (city == null)
            {
                _status = "城市已消失、易主或选中对象变化，命令未执行";
                RefreshCityWindow();
                return;
            }
            global::Kingdom kingdom = city.kingdom;
            if (city.getPopulationPeople() < 1)
            {
                _status = "城市没有人口，不能设为王都";
            }
            else if (kingdom.capital == city)
            {
                _status = city.name + " 已是王都，未重复执行";
            }
            else
            {
                try
                {
                    kingdom.setCapital(city);
                    _status = kingdom.capital == city
                        ? "原版国家已将 " + city.name + " 设为王都"
                        : "原版对象未确认王都变化";
                    LogInfo("LordWar native capital result city=" + city.getID() +
                        " success=" + (kingdom.capital == city));
                }
                catch (Exception error)
                {
                    _status = "王都命令失败：" + error.Message;
                    LogInfo("LordWar native capital FAILED: " + error);
                }
            }
            RefreshCityWindow();
        }

        private static void AddText(Transform parent, string value, float height)
        {
            var item = new GameObject("LordWarCityText");
            item.transform.SetParent(parent, false);
            var layout = item.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            var label = item.AddComponent<Text>();
            label.font = LocalizedTextManager.current_font;
            label.fontSize = 13;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;
            label.text = value;
        }

        private static void AddAction(Transform parent, string label, Action action)
        {
            var item = new GameObject("LordWarCityAction");
            item.transform.SetParent(parent, false);
            var layout = item.AddComponent<LayoutElement>();
            layout.preferredHeight = 42f;
            var image = item.AddComponent<Image>();
            image.sprite = SpriteTextureLoader.getSprite("ui/special/windowInnerSliced");
            if (image.sprite != null) image.type = Image.Type.Sliced;
            image.color = new Color(0.26f, 0.32f, 0.26f, 1f);
            var button = item.AddComponent<Button>();
            button.onClick.AddListener(() => action());

            var textObject = new GameObject("Label");
            textObject.transform.SetParent(item.transform, false);
            var text = textObject.AddComponent<Text>();
            text.font = LocalizedTextManager.current_font;
            text.fontSize = 13;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = label;
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
