using System;
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
        private ScrollWindow _cityWindow;
        private long _cityId = -1;
        private long _kingdomId = -1;
        private long _actorId = -1;
        private string _status = "请选择原版城市";

        protected override void OnModLoad()
        {
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
                var tab = TabManager.CreateTab("lordwar", "领主战争", "在原版城市中执行领主命令", cityIcon);

                AssetManager.powers.add(new GodPower
                {
                    id = SelectPowerId,
                    name = "领主战争 · 地图选城",
                    force_map_mode = MetaType.City,
                    click_special_action = IL2CPPHelper.C<PowerActionWithID>(
                        (Func<global::WorldTile, string, bool>)SelectCityFromMap),
                    unselect_when_window = true
                });
                var select = PowerButtonCreator.CreateGodPowerButton(SelectPowerId, cityIcon, tab.transform);
                PowerButtonCreator.AddButtonToTab(select, tab);

                var open = PowerButtonCreator.CreateSimpleButton(
                    "lordwar_open_city", (Action)OpenSelectedCity, cityIcon, tab.transform);
                PowerButtonCreator.AddButtonToTab(open, tab);
                LogInfo("LordWar native city entry registered");
            }
            catch (Exception error)
            {
                LogInfo("LordWar native entry FAILED: " + error);
                throw;
            }
        }

        private bool SelectCityFromMap(global::WorldTile tile, string powerId)
        {
            global::City city = tile == null ? null : tile.zone_city;
            if (!BindCity(city)) return false;
            ShowCityWindow();
            return true;
        }

        private void OpenSelectedCity()
        {
            BindCity(SelectedMetas.selected_city);
            ShowCityWindow();
        }

        private bool BindCity(global::City city)
        {
            if (city == null || city.isRekt() || city.kingdom == null)
            {
                _cityId = -1;
                _kingdomId = -1;
                _actorId = -1;
                _status = "请在原版地图选择一座仍属于国家的城市";
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
                if (city.kingdom == null || city.kingdom.getID() != _kingdomId) return null;
                return city;
            }
            return null;
        }

        private static bool BelongsToCity(global::Actor actor, global::City city)
        {
            if (actor == null || actor.isRekt() || !actor.isSapient() || !actor.isAdult())
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
            global::City city = ResolveCity();
            if (city == null)
            {
                _status = "选中城市不存在、已易主或当前地图已改变；请重新选城";
                AddText(content, _status, 78f);
                return;
            }

            global::Kingdom kingdom = city.kingdom;
            AddText(content, "城市：" + city.name + "    国家：" + kingdom.name, 56f);
            AddText(content,
                "原版人口：" + city.getPopulationPeople() +
                "    城市金币：" + city.getResourcesAmount("gold") +
                "    王都：" + (kingdom.capital == city ? "是" : "否"), 56f);

            global::Army army = city.army;
            AddText(content, "原版城主：" + (city.leader == null ? "未任命" : city.leader.getName()) +
                "    军队长：" + (army == null || army.getCaptain() == null
                    ? "没有可任命的原版军队长" : army.getCaptain().getName()), 56f);
            global::Actor selected = ResolveActor(city);
            AddText(content, "当前人物：" + (selected == null ? "未选择" : selected.getName()) +
                "（仅从原版城市居民中选择）", 42f);

            AddText(content, "原版人物（优先显示士兵，最多 16 位）：", 35f);
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
            AddAction(content, "设为王都", SetCapital);
            AddAction(content, "任命所选人物为城主", AppointCityLeader);
            AddAction(content, "任命所选士兵为军队长", AppointArmyCaptain);
            AddText(content, _status, 65f);
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
