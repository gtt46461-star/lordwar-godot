using System;
using System.Collections.Generic;
using LordWar.Data;

namespace LordWar.Military {
    /// <summary>
    /// Converts the V8/V9 unit-role descriptions into concrete equipment definitions.
    /// Abstract data such as “地区制式武器+地区护甲” is resolved deterministically
    /// from unit role and city culture instead of silently producing empty slots.
    /// </summary>
    public static class UnitLoadoutResolver {
        static readonly string[] Separators = { "+", "＋" };

        public static List<string> ResolveBasic(UnitDef unit, City city) {
            var result = new List<string>();
            if (unit == null) return result;
            string raw = unit.Gear ?? "";
            if (!IsAbstract(raw)) {
                ParseConcrete(raw, result);
                AddEnvironmentFootwear(city, result);
                AddCavalryTack(unit.Category, unit.Role, unit.Name, result);
                return Distinct(result);
            }

            string text = (unit.Name ?? "") + " " + (unit.Category ?? "") + " " + (unit.Role ?? "") + " " + (unit.Terrain ?? "");
            bool cavalry = ContainsAny(text, "骑", "马军", "游骑");
            bool ranged = ContainsAny(text, "弓", "弩", "射", "远程");
            bool crossbow = ContainsAny(text, "弩", "强弩");
            bool shield = ContainsAny(text, "盾", "守线", "城防");
            bool spear = ContainsAny(text, "枪", "矛", "槊", "枪矛");
            bool heavy = ContainsAny(text, "重", "甲士", "精锐", "近卫", "禁军");
            bool scout = ContainsAny(text, "斥候", "侦察", "游哨", "轻装");
            bool engineer = ContainsAny(text, "工程", "工兵", "筑城");
            bool support = ContainsAny(text, "旗", "号角", "医疗", "辅兵", "辎重");

            if (cavalry) {
                if (ranged) { result.Add(crossbow ? "强弩" : "短弓"); result.Add("军刀"); }
                else { result.Add(heavy ? "长枪" : "短矛"); result.Add("军刀"); }
                result.Add(heavy ? "骑兵甲" : "皮甲");
                if (heavy) result.Add("骑兵盔");
            } else if (ranged) {
                result.Add(crossbow ? "强弩" : "长弓");
                result.Add("匕首");
                result.Add(heavy ? "鳞甲" : "皮甲");
            } else if (engineer) {
                result.Add("短剑"); result.Add("皮甲");
            } else if (support) {
                result.Add("短剑"); result.Add("布衣");
            } else if (spear) {
                result.Add(heavy ? "长枪" : "长矛");
                if (shield) result.Add(heavy ? "重塔盾" : "木圆盾");
                result.Add(heavy ? "鳞甲" : "皮甲");
                if (heavy) result.Add("铁盔");
            } else if (shield) {
                result.Add("长剑"); result.Add(heavy ? "重塔盾" : "铁边圆盾");
                result.Add(heavy ? "重板甲" : "鳞甲"); result.Add("铁盔");
            } else if (scout) {
                result.Add("短剑"); result.Add("皮甲");
            } else {
                result.Add(heavy ? "长剑" : "长矛");
                result.Add(heavy ? "鳞甲" : "皮甲");
                if (heavy) result.Add("铁盔");
            }

            AddCultureVariant(city, ranged, cavalry, heavy, result);
            AddEnvironmentFootwear(city, result);
            AddCavalryTack(unit.Category, unit.Role, unit.Name, result);
            return Distinct(result);
        }

        public static List<string> ResolveSpecial(SpecialUnitDef unit, City city) {
            var result = new List<string>();
            if (unit == null) return result;
            string raw = unit.Gear ?? "";
            if (!IsAbstract(raw)) ParseConcrete(raw, result);
            if (result.Count == 0) {
                var proxy = new UnitDef { Name = unit.Name, Category = unit.Category, Role = unit.Role, Terrain = unit.Behavior, Gear = "优良以上专属套装" };
                result.AddRange(ResolveBasic(proxy, city));
            }
            AddCavalryTack(unit.Category, unit.Role, unit.Name, result);
            return Distinct(result);
        }

        public static string Join(List<string> pieces) { return pieces == null ? "" : string.Join("+", pieces.ToArray()); }

        public static bool RequiresHorse(UnitDef unit) {
            if (unit == null) return false;
            if (unit.HorseCost > 0) return true;
            return ContainsAny((unit.Name ?? "") + (unit.Category ?? "") + (unit.Role ?? ""), "骑", "马军", "游骑");
        }

        static bool IsAbstract(string raw) {
            if (string.IsNullOrWhiteSpace(raw)) return true;
            return ContainsAny(raw, "地区制式", "专属套装", "核心装备", "制式为主", "制式以上", "优良以上", "少量精良");
        }

        static void ParseConcrete(string raw, List<string> result) {
            if (string.IsNullOrWhiteSpace(raw)) return;
            string normalized = raw.Replace("＋", "+");
            foreach (string part in normalized.Split('+')) {
                string token = (part ?? "").Trim();
                if (token.Length == 0) continue;
                int slash = token.IndexOf('/');
                if (slash > 0) token = token.Substring(0, slash).Trim();
                token = token.Replace("轻装", "布衣").Replace("医疗包", "护符").Replace("旗帜", "仪仗盾").Replace("号角", "护符");
                if (token == "轻马" || token == "战马" || token == "中甲马" || token == "重甲马") continue;
                if (token == "工具") token = "短剑";
                if (token.Length > 0) result.Add(token);
            }
        }

        static void AddCultureVariant(City city, bool ranged, bool cavalry, bool heavy, List<string> result) {
            string c = city == null ? "" : (city.CultureId ?? "");
            if (c.IndexOf("寒") >= 0) ReplaceArmor(result, heavy ? "重板甲" : "御寒甲");
            else if (c.IndexOf("森林") >= 0 && ranged) ReplacePrimary(result, "长弓");
            else if (c.IndexOf("草原") >= 0 && cavalry) ReplacePrimary(result, heavy ? "长枪" : "军刀");
            else if (c.IndexOf("河") >= 0 && ranged) ReplacePrimary(result, "强弩");
            else if (c.IndexOf("山") >= 0 && !cavalry && heavy) ReplaceArmor(result, "鳞甲");
        }

        static void AddEnvironmentFootwear(City city, List<string> result) {
            string c = city == null ? "" : (city.CultureId ?? "");
            if (c.IndexOf("寒") >= 0 || c.IndexOf("雪") >= 0) result.Add("雪地靴");
            else if (c.IndexOf("沙") >= 0 || c.IndexOf("荒") >= 0) result.Add("沙地靴");
            else result.Add("军靴");
        }

        static void AddCavalryTack(string category, string role, string name, List<string> result) {
            string text = (category ?? "") + (role ?? "") + (name ?? "");
            if (!ContainsAny(text, "骑", "马军", "游骑")) return;
            bool heavy = ContainsAny(text, "重", "甲", "近卫", "禁军");
            result.Add(heavy ? "重骑鞍" : "军鞍");
            if (heavy) result.Add("马面甲");
        }

        static void ReplaceArmor(List<string> result, string replacement) {
            for (int i = 0; i < result.Count; i++) if (ContainsAny(result[i], "甲", "衣")) { result[i] = replacement; return; }
            result.Add(replacement);
        }

        static void ReplacePrimary(List<string> result, string replacement) {
            if (result.Count == 0) { result.Add(replacement); return; }
            result[0] = replacement;
        }

        static List<string> Distinct(List<string> input) {
            var seen = new HashSet<string>(); var output = new List<string>();
            foreach (string s in input) if (!string.IsNullOrWhiteSpace(s) && seen.Add(s)) output.Add(s);
            return output;
        }

        static bool ContainsAny(string s, params string[] keys) {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (string k in keys) if (s.IndexOf(k, StringComparison.Ordinal) >= 0) return true;
            return false;
        }
    }
}
