"""Bake editable Godot UI scenes. No layout is created by the game at runtime."""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1] / "airwar-together"
parts = []


def node(name, kind, parent=".", **props):
    parts.append(f'\n[node name="{name}" type="{kind}"' + (f' parent="{parent}"' if parent else "") + ']')
    for key, value in props.items():
        if isinstance(value, str) and value.startswith(("Color(", "Vector2(")):
            value = re.sub(r"(?<![\w.])\.(\d)", r"0.\1", value)
        parts.append(f'{key.replace("__", "/")} = {value}')


def q(text):
    return '"' + text.replace('\\', '\\\\').replace('"', '\\"').replace('\n', '\\n') + '"'


def full(name, kind="Control", parent=".", **props):
    node(name, kind, parent, layout_mode=3 if not parent else 1, anchors_preset=15, anchor_right=1.0, anchor_bottom=1.0,
         grow_horizontal=2, grow_vertical=2, mouse_filter=2, **props)


def label(name, parent, text, font=18, **props):
    node(name, "Label", parent, layout_mode=2, mouse_filter=2, text=q(text), theme_override_font_sizes__font_size=font, **props)


def button(name, parent, text, height=46, primary=False, **props):
    values = dict(layout_mode=2, custom_minimum_size=f"Vector2(0, {height})", text=q(text), focus_mode=0,
                  theme_type_variation='&"PrimaryButton"' if primary else '&""')
    values.update(props)
    node(name, "Button", parent, **values)


def rect_props(left, top, right, bottom):
    return dict(offset_left=left, offset_top=top, offset_right=right, offset_bottom=bottom)


def bake():
    parts.clear()
    parts.extend(['[gd_scene load_steps=7 format=3]',
                  '[ext_resource type="Script" path="res://scripts/Arena/ArenaHud.cs" id="script"]',
                  '[ext_resource type="Theme" path="res://assets/ui/FlightTheme.tres" id="theme"]',
                  '[ext_resource type="StyleBox" path="res://assets/ui/modal_panel.tres" id="panel"]',
                  '[ext_resource type="StyleBox" path="res://assets/ui/arena_bar_health.tres" id="health"]',
                  '[ext_resource type="StyleBox" path="res://assets/ui/arena_bar_energy.tres" id="energy"]',
                  '[ext_resource type="StyleBox" path="res://assets/ui/arena_bar_background.tres" id="bar_bg"]'])
    full("ArenaHud", parent="", script='ExtResource("script")', theme='ExtResource("theme")')
    full("Lobby")
    node("Tint", "ColorRect", "Lobby", layout_mode=1, anchor_right=.49, anchor_bottom=1.0,
         color="Color(0.018, 0.04, 0.065, .89)", mouse_filter=2)
    node("Panel", "MarginContainer", "Lobby", layout_mode=1, anchor_right=.48, anchor_bottom=1.0,
         theme_override_constants__margin_left=40, theme_override_constants__margin_right=30,
         theme_override_constants__margin_top=25, theme_override_constants__margin_bottom=22)
    node("Column", "VBoxContainer", "Lobby/Panel", layout_mode=2, theme_override_constants__separation=7)
    col = "Lobby/Panel/Column"
    label("Brand", col, "A / T     ORBITAL FRONTIER", 16, theme_override_colors__font_color="Color(.44,.87,.92,1)")
    label("Title", col, "星际空战", 42)
    label("Subtitle", col, "32 人房间 · 四大战区 · 自由飞行与跃迁", 16)
    label("PlayerLabel", col, "飞行员呼号", 14)
    node("Callsign", "LineEdit", col, layout_mode=2, custom_minimum_size="Vector2(0, 38)", text=q("Pilot"), max_length=18)
    node("Ship", "OptionButton", col, layout_mode=2, custom_minimum_size="Vector2(0, 38)")
    label("ServerLabel", col, "服务器 IP / 域名", 14)
    node("Address", "LineEdit", col, layout_mode=2, custom_minimum_size="Vector2(0, 38)", text=q("127.0.0.1"), max_length=253)
    node("Port", "LineEdit", col, layout_mode=2, custom_minimum_size="Vector2(0, 34)", text=q("24567"), max_length=5, placeholder_text=q("UDP 端口"))
    node("Sector", "OptionButton", col, layout_mode=2, custom_minimum_size="Vector2(0, 36)")
    button("Host", col, "创建房间 / 本机试飞", 46, True)
    button("Join", col, "连接房间    →", 42)
    button("Back", col, "返回 2.5D 生存模式", 36)
    label("Status", col, "同一 Wi-Fi 填主机局域网 IP；互联网直连需要开放 UDP 端口。", 13,
          autowrap_mode=2, custom_minimum_size="Vector2(300, 40)")
    label("LocalAddress", col, "", 13, autowrap_mode=2)
    label("WorldHint", "Lobby", "探索 · 追击 · 跃迁\n\n荒漠峡谷 / 冰海群岛 / 火山工业区 / 深空航道", 18,
          anchor_left=.55, anchor_right=.96, anchor_top=.79, anchor_bottom=.94, horizontal_alignment=2,
          theme_override_colors__font_shadow_color="Color(0,0,0,1)", theme_override_constants__shadow_offset_x=2,
          theme_override_constants__shadow_offset_y=2)

    full("Flight", visible="false")
    node("Top", "HBoxContainer", "Flight", layout_mode=1, anchor_right=1.0, offset_left=22.0, offset_top=16.0,
         offset_right=-22.0, offset_bottom=60.0, theme_override_constants__separation=16)
    label("Room", "Flight/Top", "ORBITAL FRONTIER  /  01/32", 15, size_flags_horizontal=3)
    label("Sector", "Flight/Top", "阿斯特里昂 · 峡谷前哨", 18, size_flags_horizontal=3, horizontal_alignment=2)
    button("Menu", "Flight/Top", "菜单", 42, custom_minimum_size="Vector2(84, 42)")
    label("Telemetry", "Flight", "0540 km/h\n高度 0350 m\nX 0.0 Z 0.0 km", 20, **rect_props(28,90,285,205))
    label("Vitals", "Flight", "机体 100 护盾 50\n能量 100 击落 0 / 损失 0", 18, **rect_props(28,215,350,275))
    for name, top, color in [("Health", 284, "Color(.32,.85,.8,1)"), ("Energy", 301, "Color(.4,.65,1,1)")]:
        node(name, "ProgressBar", "Flight", layout_mode=0, **rect_props(28,top,245,top+9), value=100.0, show_percentage="false", mouse_filter=2,
             theme_override_styles__background='ExtResource("bar_bg")', theme_override_styles__fill=f'ExtResource("{name.lower()}")')
    label("Help", "Flight", "右键转向 · 空格射击 · Shift 推进 · J 跃迁 · V 视角", 13,
          anchor_top=1.0, anchor_bottom=1.0, **rect_props(25,-30,-25,-8), anchor_right=1.0)
    label("Respawn", "Flight", "", 30, anchor_left=.2, anchor_right=.8, anchor_top=.36, anchor_bottom=.54,
          horizontal_alignment=1, vertical_alignment=1)
    node("Navigation", "VBoxContainer", "Flight", layout_mode=1, anchor_left=1.0, anchor_right=1.0,
         **rect_props(-330,90,-22,260), theme_override_constants__separation=7)
    node("Destination", "OptionButton", "Flight/Navigation", layout_mode=2, custom_minimum_size="Vector2(0, 42)", focus_mode=0)
    button("Warp", "Flight/Navigation", "跃迁 / J", 42)
    label("Hint", "Flight/Navigation", "目的地 / 选择战区后充能跃迁", 13, autowrap_mode=2)
    node("Throttle", "VBoxContainer", "Flight", layout_mode=1, anchor_left=.4, anchor_right=.6, anchor_top=1.0, anchor_bottom=1.0,
         **rect_props(0,-110,0,-60))
    label("Label", "Flight/Throttle", "推力 / THROTTLE", 13, horizontal_alignment=1)
    node("Slider", "HSlider", "Flight/Throttle", layout_mode=2, custom_minimum_size="Vector2(0, 28)", min_value=0.0, max_value=1.0, step=.01, value=.5)
    for name, text, bounds in [
        ("Fire", "开火", (-142,-198,-24,-84)), ("Boost", "推进", (-265,-160,-153,-84)),
        ("RollRight", "右滚", (-128,-280,-24,-212)), ("RollLeft", "左滚", (-245,-250,-141,-178)),
    ]:
        button(name, "Flight", text, 60, anchor_left=1.0, anchor_right=1.0, anchor_top=1.0, anchor_bottom=1.0,
               **rect_props(*bounds))
    button("Camera", "Flight", "视角 / V", 40, anchor_left=1.0, anchor_right=1.0,
           anchor_top=1.0, anchor_bottom=1.0, **rect_props(-142,-74,-24,-34))
    button("Scores", "Flight", "战绩", 40, anchor_top=1.0, anchor_bottom=1.0, **rect_props(28,-78,125,-38))
    label("Scoreboard", "Flight", "", 15, anchor_left=.3, anchor_right=.7, anchor_top=.1, anchor_bottom=.9,
          horizontal_alignment=1, theme_override_colors__font_shadow_color="Color(0,0,0,1)",
          theme_override_constants__shadow_offset_x=2, theme_override_constants__shadow_offset_y=2, visible="false")

    full("Menu", visible="false")
    node("Shade", "ColorRect", "Menu", layout_mode=1, anchor_right=1.0, anchor_bottom=1.0,
         color="Color(.015,.025,.04,.8)", mouse_filter=0)
    full("Center", "CenterContainer", "Menu")
    node("Panel", "PanelContainer", "Menu/Center", layout_mode=2, custom_minimum_size="Vector2(470, 0)")
    node("Column", "VBoxContainer", "Menu/Center/Panel", layout_mode=2, theme_override_constants__separation=15)
    label("Title", "Menu/Center/Panel/Column", "飞行菜单", 34)
    label("Hint", "Menu/Center/Panel/Column", "房间仍在运行；打开菜单期间停止操纵与射击。", 15)
    button("Resume", "Menu/Center/Panel/Column", "继续飞行", 54, True)
    button("Leave", "Menu/Center/Panel/Column", "退出房间", 48)
    button("Back", "Menu/Center/Panel/Column", "返回 2.5D 生存模式", 48)
    label("Notice", ".", "", 19, anchor_left=.2, anchor_right=.8, anchor_top=.09, anchor_bottom=.17,
          horizontal_alignment=1, autowrap_mode=2, visible="false", theme_override_colors__font_color="Color(.5,.95,.92,1)",
          theme_override_colors__font_shadow_color="Color(0,0,0,1)", theme_override_constants__shadow_offset_x=2, theme_override_constants__shadow_offset_y=2)
    target = ROOT / "scenes/arena/ArenaHud.tscn"
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text("\n".join(parts) + "\n", encoding="utf-8")


if __name__ == "__main__":
    bake()
