#!/usr/bin/env python3
"""Inventory legacy LordWar data against the WorldBox adapter status."""

import argparse
import csv
import hashlib
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
DATA = ROOT / "LordWarMod" / "Data"
EXPECTED = {
    "units_v8.csv": 156,
    "special_units_v8.csv": 120,
    "commander_skills_v8.csv": 360,
    "policies_v8.csv": 300,
}
KEY_FIELDS = {
    "units_v8.csv": ("id", "兵种名称"),
    "special_units_v8.csv": ("ID", "特殊兵种名称"),
    "commander_skills_v8.csv": ("技能ID", "技能名称"),
    "policies_v8.csv": ("政策ID", "政策名称"),
}
ROUTES = {
    "units_v8.csv": "原版人类Actor职业、Equipment/Item、特质与战斗行为",
    "special_units_v8.csv": "原版人类Actor职业、Equipment/Item、特质与战斗行为；保留审批和解锁条件",
    "commander_skills_v8.csv": "原版Actor特质、Army队长、原版战斗/决策事件",
    "policies_v8.csv": "稳定原版City/Kingdom ID上的政策状态、审批队列与真实原版资源/命令效果",
    "equipment_types_v9.csv": "原版EquipmentAsset/Item及真实Actor装备槽",
    "equipment_quality_v7.csv": "原版Item属性、真实制造资源与维护成本",
    "general_traits_v8.csv": "原版Actor特质与Army队长行为",
    "official_traits_v8.csv": "原版Actor特质、City政务审批和可验证政策效果",
    "soldier_traits_v8.csv": "原版Actor特质与原版攻击/生命/移动/死亡事件",
    "general_special_unlock_v8.csv": "审批通过后解锁原版人类职业/装备/特质组合",
    "official_doctrine_unlock_v8.csv": "官员资格、固定政务箱审批与可观测的原版对象效果",
    "family_traditions_v7.csv": "WorldBox原生Clan/Actor ID或附着稳定ID的扩展状态；不创建第二套人物",
    "family_names_v8.csv": "原版Actor/Clan名称字段及稳定ID关联",
    "city_cultures_v7.csv": "WorldBox原生Culture/Kingdom/Building资源；逐项核对实际可用资产",
    "person_traits_v8.csv": "原版Actor特质/职业和真实行为回调",
    "soldier_promotion_v7.csv": "真实原版Actor职业、装备与资格审批",
    "person_names_v8.csv": "原版Actor姓名显示与稳定Actor ID",
    "city_names_v8.csv": "原版City名称字段与稳定City ID",
    "ai_difficulty_v7.csv": "与玩家相同资格、成本、申请/批准和原版对象命令入口",
    "combat_params_v9.json": "仅通过原版Actor战斗/生命/死亡入口提交一次",
    "construction_params_v9.json": "原版City建筑订单、Building与真实库存扣款",
    "population_economy_params_v9.json": "原版City人口、真实资源和原版人类Actor状态",
    "map_params_v9.json": "只读原版地图和City；不创建独立GameWorld",
}
FIELDS = (
    "source_file",
    "source_sha256",
    "source_row",
    "source_id",
    "source_title",
    "planned_worldbox_route",
    "status",
    "runtime_file_or_callsite",
    "input_record_json",
)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(output):
    rows = []
    counts = {}
    for path in sorted(DATA.iterdir()):
        if path.suffix.lower() == ".csv":
            with path.open("r", encoding="utf-8-sig", newline="") as handle:
                records = list(csv.DictReader(handle))
            counts[path.name] = len(records)
            if path.name in EXPECTED and len(records) != EXPECTED[path.name]:
                raise ValueError(f"{path.name}: expected {EXPECTED[path.name]} records, found {len(records)}")
            key_field, title_field = KEY_FIELDS.get(path.name, ("ID", "名称"))
            for number, record in enumerate(records, 1):
                rows.append({
                    "source_file": str(path.relative_to(ROOT)),
                    "source_sha256": digest(path),
                    "source_row": number + 1,
                    "source_id": record.get(key_field) or f"row-{number:04d}",
                    "source_title": record.get(title_field) or "",
                    "planned_worldbox_route": ROUTES.get(path.name, "需先核对原版Actor/City/Kingdom/Building接口再决定适配去向"),
                    "status": "NOT_STARTED",
                    "runtime_file_or_callsite": "none; this inventory is not included in the active mod payload",
                    "input_record_json": json.dumps(record, ensure_ascii=False, separators=(",", ":")),
                })
        elif path.suffix.lower() == ".json":
            value = json.loads(path.read_text(encoding="utf-8"))
            leaves = []

            def visit(node, prefix="$"):
                if isinstance(node, dict):
                    for key, child in node.items():
                        visit(child, prefix + "." + str(key))
                elif isinstance(node, list):
                    for index, child in enumerate(node):
                        visit(child, prefix + f"[{index}]")
                else:
                    leaves.append((prefix, node))

            visit(value)
            counts[path.name] = len(leaves)
            for number, (key, item) in enumerate(leaves, 1):
                rows.append({
                    "source_file": str(path.relative_to(ROOT)),
                    "source_sha256": digest(path),
                    "source_row": number,
                    "source_id": key,
                    "source_title": str(item),
                    "planned_worldbox_route": ROUTES.get(path.name, "需先核对原版对象和调用签名再决定适配去向"),
                    "status": "NOT_STARTED",
                    "runtime_file_or_callsite": "none; this inventory is not included in the active mod payload",
                    "input_record_json": json.dumps({"path": key, "value": item}, ensure_ascii=False, separators=(",", ":")),
                })

    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=FIELDS, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)
    print(json.dumps({"rows": len(rows), "source_counts": counts, "output": str(output)}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    build(parser.parse_args().output)
