extends Control

const CARD_SIZE := Vector2(116, 174)
const HOVER_Y := -20.0
const SELECT_Y := -42.0
const MOTION_TIME := 0.18

const BG := Color("#101316")
const PANEL := Color(0.055, 0.075, 0.082, 0.92)
const TEXT := Color("#f3ead9")
const MUTED := Color("#b8ad9b")
const GOLD := Color("#e0b66e")
const RED := Color("#c97869")

var hand_cards = [
	["群羊", "1", "res://assets/cards/sheep-1.png", "物資", "基礎數值 1。連續打出 3 張群羊可觸發成群。"],
	["糧食", "3", "res://assets/cards/food-3.png", "物資", "基礎數值 3。連續打出 3 張糧食可觸發播種。"],
	["金錢", "2", "res://assets/cards/money-2.png", "物資", "基礎數值 2。連續打出 3 張金錢可觸發積累。"],
	["恩典", "3", "res://assets/cards/grace-3.png", "特殊物資", "行動中包含恩典時，額外成全一次可對應的法則。"],
	["群羊", "1", "res://assets/cards/sheep-1.png", "物資", "基礎數值 1。連續打出 3 張群羊可觸發成群。"],
	["糧食", "3", "res://assets/cards/food-3.png", "物資", "基礎數值 3。連續打出 3 張糧食可觸發播種。"],
	["金錢", "2", "res://assets/cards/money-2.png", "物資", "基礎數值 2。連續打出 3 張金錢可觸發積累。"],
]

var selected_card: TextureButton
var card_home := {}
var card_tweens := {}
var card_slots := {}
var hand_cards_nodes: Array[TextureButton] = []
var inspector: Panel
var inspector_card: TextureRect
var inspector_text: VBoxContainer

func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_PASS
	_build_vertical_slice()
	print("Shepherd Reboot visual slice ready.")

func panel_style(color: Color, radius := 16, border := Color(0.7, 0.58, 0.36, 0.42), width := 1) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = color
	style.corner_radius_top_left = radius
	style.corner_radius_top_right = radius
	style.corner_radius_bottom_left = radius
	style.corner_radius_bottom_right = radius
	style.border_width_left = width
	style.border_width_top = width
	style.border_width_right = width
	style.border_width_bottom = width
	style.border_color = border
	style.content_margin_left = 16
	style.content_margin_right = 16
	style.content_margin_top = 12
	style.content_margin_bottom = 12
	return style

func make_label(value: String, font_px: int, color := TEXT) -> Label:
	var label := Label.new()
	label.text = value
	label.add_theme_font_size_override("font_size", font_px)
	label.add_theme_color_override("font_color", color)
	return label

func add_panel(parent: Node, position: Vector2, size: Vector2, color := PANEL, border := Color(0.7, 0.58, 0.36, 0.42), radius := 16) -> Panel:
	var panel := Panel.new()
	panel.position = position
	panel.size = size
	panel.add_theme_stylebox_override("panel", panel_style(color, radius, border, 1))
	parent.add_child(panel)
	return panel

func add_rule(parent: Node, position: Vector2, size: Vector2, color := Color(0.76, 0.63, 0.40, 0.42)) -> ColorRect:
	var rule := ColorRect.new()
	rule.position = position
	rule.size = size
	rule.color = color
	parent.add_child(rule)
	return rule

func make_progress(maximum: float, current: float, fill_color: Color, size: Vector2) -> ProgressBar:
	var bar := ProgressBar.new()
	bar.custom_minimum_size = size
	bar.size = size
	bar.max_value = maximum
	bar.value = current
	bar.show_percentage = false
	bar.add_theme_stylebox_override("background", panel_style(Color(0.03, 0.04, 0.045, 0.88), 8, Color(0.3, 0.34, 0.34, 0.7), 1))
	bar.add_theme_stylebox_override("fill", panel_style(fill_color, 8, fill_color.lightened(0.2), 1))
	return bar

func make_nav_button(title: String) -> Button:
	var button := Button.new()
	button.text = title
	button.custom_minimum_size = Vector2(84, 38)
	button.flat = true
	button.add_theme_font_size_override("font_size", 15)
	button.add_theme_color_override("font_color", MUTED)
	button.add_theme_color_override("font_hover_color", TEXT)
	button.add_theme_stylebox_override("hover", panel_style(Color(0.18, 0.15, 0.11, 0.74), 10, Color(0.84, 0.68, 0.39, 0.55), 1))
	return button

func _build_vertical_slice() -> void:
	var base := ColorRect.new()
	base.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	base.color = BG
	base.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(base)
	var scene_bg := TextureRect.new()
	scene_bg.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	scene_bg.texture = load("res://assets/backgrounds/wilderness-awakening.jpg")
	scene_bg.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	scene_bg.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	scene_bg.modulate = Color(0.82, 0.83, 0.78, 0.94)
	scene_bg.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(scene_bg)
	var veil := ColorRect.new()
	veil.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	veil.color = Color(0.025, 0.035, 0.043, 0.20)
	veil.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(veil)
	_build_header()
	_build_player_panel()
	_build_hand()
	_build_inspector()

func _build_header() -> void:
	var header := Control.new()
	header.position = Vector2(28, 20)
	header.size = Vector2(1224, 54)
	header.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(header)
	var nav := HBoxContainer.new()
	nav.position = Vector2(1032, 8)
	nav.size = Vector2(190, 44)
	nav.alignment = BoxContainer.ALIGNMENT_END
	nav.add_theme_constant_override("separation", 10)
	nav.mouse_filter = Control.MOUSE_FILTER_PASS
	header.add_child(nav)
	for item in [["⌂", "地圖"], ["✧", "探索"], ["⚙", "設定"]]:
		var button := Button.new()
		button.text = item[0]
		button.tooltip_text = item[1]
		button.custom_minimum_size = Vector2(48, 42)
		button.flat = true
		button.add_theme_font_size_override("font_size", 24)
		button.add_theme_color_override("font_color", MUTED)
		button.add_theme_color_override("font_hover_color", TEXT)
		button.add_theme_stylebox_override("hover", panel_style(Color(0.18, 0.15, 0.11, 0.44), 12, Color(0.84, 0.68, 0.39, 0.42), 1))
		nav.add_child(button)
func _build_player_panel() -> void:
	var panel := Panel.new()
	panel.set_anchors_preset(Control.PRESET_TOP_LEFT)
	panel.position = Vector2(20, 20)
	panel.size = Vector2(292, 118)
	panel.add_theme_stylebox_override("panel", panel_style(Color(0.025, 0.028, 0.028, 0.70), 14, Color(0.30, 0.25, 0.19, 0.72), 1))
	add_child(panel)
	var content := HBoxContainer.new()
	content.position = Vector2(10, 10)
	content.size = Vector2(272, 98)
	content.add_theme_constant_override("separation", 10)
	content.mouse_filter = Control.MOUSE_FILTER_IGNORE
	panel.add_child(content)
	var portrait := TextureRect.new()
	portrait.custom_minimum_size = Vector2(80, 80)
	portrait.size = Vector2(80, 80)
	var source_texture := load("res://assets/characters/character-wanderer-v1-transparent.png") as Texture2D
	var head := AtlasTexture.new()
	head.atlas = source_texture
	head.region = Rect2(400, 0, 300, 300)
	portrait.texture = head
	portrait.expand_mode = 1
	portrait.stretch_mode = 5
	portrait.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	portrait.mouse_filter = Control.MOUSE_FILTER_IGNORE
	content.add_child(portrait)
	var info := VBoxContainer.new()
	info.custom_minimum_size = Vector2(182, 98)
	info.add_theme_constant_override("separation", 3)
	info.mouse_filter = Control.MOUSE_FILTER_IGNORE
	content.add_child(info)
	var name_label := make_label("行者", 19, TEXT)
	info.add_child(name_label)
	var role := make_label("在荒野中穿行的人", 10, MUTED)
	info.add_child(role)
	var sanity_row := HBoxContainer.new()
	sanity_row.custom_minimum_size = Vector2(182, 20)
	sanity_row.add_theme_constant_override("separation", 4)
	info.add_child(sanity_row)
	var sanity_caption := make_label("理智", 11, MUTED)
	sanity_caption.custom_minimum_size = Vector2(28, 20)
	sanity_row.add_child(sanity_caption)
	var sanity_bar := make_progress(20, 20, Color("#8f4038"), Vector2(98, 9))
	sanity_bar.custom_minimum_size = Vector2(98, 9)
	sanity_bar.size = Vector2(98, 9)
	sanity_bar.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	sanity_row.add_child(sanity_bar)
	var sanity_value := make_label("20", 15, TEXT)
	sanity_value.custom_minimum_size = Vector2(24, 20)
	sanity_row.add_child(sanity_value)
	var ap_row := HBoxContainer.new()
	ap_row.custom_minimum_size = Vector2(182, 19)
	ap_row.add_theme_constant_override("separation", 4)
	info.add_child(ap_row)
	var ap_caption := make_label("行動值", 11, MUTED)
	ap_caption.custom_minimum_size = Vector2(42, 19)
	ap_row.add_child(ap_caption)
	var flame := _make_flame_icon()
	flame.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	ap_row.add_child(flame)
	for i in range(3):
		var pip := Panel.new()
		pip.custom_minimum_size = Vector2(28, 9)
		pip.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		pip.add_theme_stylebox_override("panel", panel_style(Color(0.58, 0.34, 0.16, 0.92), 4, Color(0.82, 0.57, 0.29, 0.55), 1))
		ap_row.add_child(pip)
func _make_flame_icon() -> Control:
	var holder := Control.new()
	holder.custom_minimum_size = Vector2(16, 18)
	holder.size = Vector2(16, 18)
	holder.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var flame := Polygon2D.new()
	flame.polygon = PackedVector2Array([Vector2(8, 0), Vector2(14, 7), Vector2(12, 14), Vector2(8, 18), Vector2(3, 14), Vector2(2, 8)])
	flame.color = Color("#d98a35")
	flame.antialiased = true
	holder.add_child(flame)
	return holder

func _build_hand() -> void:
	var hand_panel := Control.new()
	hand_panel.position = Vector2(0, 496)
	hand_panel.size = Vector2(1280, 224)
	hand_panel.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(hand_panel)
	var hand := HBoxContainer.new()
	hand.position = Vector2(244, 18)
	hand.size = Vector2(792, 188)
	hand.alignment = BoxContainer.ALIGNMENT_CENTER
	hand.add_theme_constant_override("separation", -10)
	hand.mouse_filter = Control.MOUSE_FILTER_PASS
	hand_panel.add_child(hand)
	for i in range(hand_cards.size()):
		var item: Array = hand_cards[i]
		var slot := Panel.new()
		slot.custom_minimum_size = CARD_SIZE
		slot.size = CARD_SIZE
		slot.mouse_filter = Control.MOUSE_FILTER_IGNORE
		slot.add_theme_stylebox_override("panel", _card_style())
		hand.add_child(slot)
		var card := TextureButton.new()
		card.position = Vector2(10, 10)
		card.custom_minimum_size = CARD_SIZE - Vector2(20, 20)
		card.size = CARD_SIZE - Vector2(20, 20)
		card.ignore_texture_size = true
		card.stretch_mode = TextureButton.STRETCH_KEEP_ASPECT_CENTERED
		card.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
		card.texture_normal = load(item[2])
		card.material = _rounded_card_material(0.065)
		card.tooltip_text = item[0] + "  " + item[1]
		slot.add_child(card)
		card_home[card] = Vector2(10, 10)
		card_slots[card] = slot
		hand_cards_nodes.append(card)
		slot.z_index = i
		card.mouse_entered.connect(func(c = card): _hover_card(c, true))
		card.mouse_exited.connect(func(c = card): _hover_card(c, false))
		card.pressed.connect(func(c = card, data = item): _select_card(c, data))
func _build_inspector() -> void:
	inspector = Panel.new()
	inspector.position = Vector2(270, 94)
	inspector.size = Vector2(740, 360)
	inspector.add_theme_stylebox_override("panel", panel_style(Color(0.02, 0.03, 0.03, 0.16), 18, Color(0, 0, 0, 0), 0))
	inspector.mouse_filter = Control.MOUSE_FILTER_IGNORE
	inspector.z_index = 50
	inspector.visible = false
	var close_hint := make_label("再次點選牌面或空白處收起", 12, MUTED)
	close_hint.position = Vector2(420, 322)
	inspector.add_child(close_hint)
	inspector_card = TextureRect.new()
	inspector_card.position = Vector2(8, 8)
	inspector_card.size = Vector2(228, 332)
	inspector_card.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	inspector_card.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	inspector_card.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	inspector_card.mouse_filter = Control.MOUSE_FILTER_IGNORE
	inspector_card.material = _rounded_card_material(0.065)
	inspector.add_child(inspector_card)
	inspector_text = VBoxContainer.new()
	inspector_text.position = Vector2(272, 42)
	inspector_text.size = Vector2(410, 220)
	inspector_text.add_theme_constant_override("separation", 10)
	inspector_text.mouse_filter = Control.MOUSE_FILTER_IGNORE
	inspector.add_child(inspector_text)

func _card_home(card: TextureButton) -> Vector2:
	return Vector2(10, 10)

func _hover_card(card: TextureButton, hovering: bool) -> void:
	if card == selected_card:
		return
	var home := _card_home(card)
	if hovering:
		card_slots[card].z_index = 1000
		_tween_card(card, home + Vector2(0, HOVER_Y), Vector2(1.025, 1.025))
	else:
		_restore_card_layer(card)
		_tween_card(card, home, Vector2.ONE)

func _select_card(card: TextureButton, data: Array) -> void:
	if selected_card == card:
		_close_inspector()
		return
	if selected_card:
		var previous: TextureButton = selected_card
		_restore_card_layer(previous)
		_tween_card(previous, _card_home(previous), Vector2.ONE)
	selected_card = card
	card_slots[card].z_index = 1100
	_tween_card(card, _card_home(card) + Vector2(0, SELECT_Y), Vector2(1.08, 1.08))
	_show_inspector(data)

func _show_inspector(data: Array) -> void:
	inspector_card.texture = load(data[2])
	for child in inspector_text.get_children():
		child.queue_free()
	var overline := make_label(data[3].to_upper(), 12, GOLD)
	inspector_text.add_child(overline)
	var title := make_label(data[0] + "  ·  " + data[1], 30, TEXT)
	inspector_text.add_child(title)
	var rule := make_label("基礎數值  " + data[1], 16, Color("#e4c487"))
	inspector_text.add_child(rule)
	var desc := make_label(data[4], 17, Color("#eee5d6"))
	desc.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	desc.size = Vector2(360, 80)
	inspector_text.add_child(desc)
	var hint := make_label("這張牌將在確認行動時結算。", 13, MUTED)
	inspector_text.add_child(hint)
	inspector.modulate.a = 0.0
	inspector.visible = true
	var tween := create_tween().set_parallel(true)
	tween.set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
	tween.tween_property(inspector, "modulate:a", 1.0, 0.16)
	tween.tween_property(inspector, "position:y", 102.0, 0.18)

func _close_inspector() -> void:
	if selected_card:
		var old := selected_card
		selected_card = null
		_restore_card_layer(old)
		_tween_card(old, _card_home(old), Vector2.ONE)
	inspector.visible = false

func _restore_card_layer(card: TextureButton) -> void:
	var index: int = hand_cards_nodes.find(card)
	card_slots[card].z_index = index if index >= 0 else 0

func _tween_card(card: TextureButton, target_pos: Vector2, target_scale: Vector2) -> void:
	if card_tweens.has(card) and is_instance_valid(card_tweens[card]):
		card_tweens[card].kill()
	card.pivot_offset = card.size * 0.5
	var tween := create_tween().set_parallel(true)
	tween.set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
	tween.tween_property(card, "position", target_pos, MOTION_TIME)
	tween.tween_property(card, "scale", target_scale, MOTION_TIME)
	card_tweens[card] = tween

func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and event.pressed and selected_card:
		var local_mouse := get_local_mouse_position()
		var card_rect := Rect2(selected_card.global_position, selected_card.size * selected_card.scale)
		var inspector_rect := Rect2(inspector.global_position, inspector.size)
		if not card_rect.has_point(local_mouse) and not inspector_rect.has_point(local_mouse):
			_close_inspector()

func _card_style() -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = Color("#f7f2e8")
	style.border_width_left = 1
	style.border_width_top = 1
	style.border_width_right = 1
	style.border_width_bottom = 1
	style.border_color = Color("#333333")
	style.shadow_color = Color(0.0, 0.0, 0.0, 0.0)
	style.shadow_size = 0
	style.shadow_offset = Vector2.ZERO
	return style

func _rounded_card_material(radius: float) -> ShaderMaterial:
	var shader := Shader.new()
	shader.code = """shader_type canvas_item;
render_mode unshaded;
uniform float radius = 0.065;
uniform float edge_softness = 0.004;

float rounded_rect_alpha(vec2 uv, float r, float softness) {
    vec2 p = abs(uv - vec2(0.5));
    vec2 b = vec2(0.5 - r);
    vec2 q = p - b;
    float dist = length(max(q, vec2(0.0))) + min(max(q.x, q.y), 0.0) - r;
    return 1.0 - smoothstep(-softness, softness, dist);
}

void fragment() {
    vec4 tex = texture(TEXTURE, UV);
    float mask = rounded_rect_alpha(UV, radius, edge_softness);
    COLOR = vec4(tex.rgb * COLOR.rgb, tex.a * COLOR.a * mask);
}"""
	var material := ShaderMaterial.new()
	material.shader = shader
	material.set_shader_parameter("radius", radius)
	material.set_shader_parameter("edge_softness", 0.003)
	return material
