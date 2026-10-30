extends Control

const CARD_SIZE := Vector2(126, 188)
const HOVER_Y := -14.0
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
	scene_bg.modulate = Color(0.72, 0.73, 0.68, 0.88)
	scene_bg.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(scene_bg)
	var veil := ColorRect.new()
	veil.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	veil.color = Color(0.025, 0.035, 0.043, 0.38)
	veil.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(veil)
	_build_header()
	_build_player_panel()
	_build_encounter_panel()
	_build_enemy_panel()
	_build_hand()
	_build_inspector()

func _build_header() -> void:
	var header := add_panel(self, Vector2(28, 22), Vector2(1224, 70), Color(0.035, 0.045, 0.05, 0.82), Color(0.71, 0.57, 0.35, 0.38), 18)
	var title := make_label("牧羊人：荒野之歌", 25, TEXT)
	title.position = Vector2(22, 10)
	header.add_child(title)
	var subtitle := make_label("REBOOT  ·  第一章／荒野邊界", 12, MUTED)
	subtitle.position = Vector2(24, 43)
	header.add_child(subtitle)
	var state := make_label("遭遇準備中", 14, GOLD)
	state.position = Vector2(420, 25)
	header.add_child(state)
	add_rule(header, Vector2(400, 21), Vector2(1, 28), Color(0.74, 0.62, 0.41, 0.48))
	var nav := HBoxContainer.new()
	nav.position = Vector2(855, 15)
	nav.size = Vector2(344, 42)
	nav.alignment = BoxContainer.ALIGNMENT_END
	nav.add_theme_constant_override("separation", 8)
	header.add_child(nav)
	for item in ["地圖", "探索", "設定"]:
		nav.add_child(make_nav_button(item))

func _build_player_panel() -> void:
	var panel := add_panel(self, Vector2(28, 112), Vector2(302, 352), Color(0.045, 0.06, 0.065, 0.88), Color(0.68, 0.55, 0.35, 0.48), 18)
	var overline := make_label("行者狀態", 12, GOLD)
	overline.position = Vector2(20, 16)
	panel.add_child(overline)
	var portrait := TextureRect.new()
	portrait.position = Vector2(20, 50)
	portrait.size = Vector2(92, 124)
	portrait.texture = load("res://assets/characters/character-wanderer-v1.png")
	portrait.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	portrait.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	portrait.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	panel.add_child(portrait)
	var name := make_label("行者", 24, TEXT)
	name.position = Vector2(132, 54)
	panel.add_child(name)
	var role := make_label("在荒野中尋路的人", 13, MUTED)
	role.position = Vector2(134, 88)
	panel.add_child(role)
	add_rule(panel, Vector2(132, 116), Vector2(140, 1), Color(0.73, 0.61, 0.42, 0.42))
	var sanity_caption := make_label("理智", 12, MUTED)
	sanity_caption.position = Vector2(132, 130)
	panel.add_child(sanity_caption)
	var sanity_value := make_label("20 / 20", 18, TEXT)
	sanity_value.position = Vector2(208, 125)
	panel.add_child(sanity_value)
	var sanity_bar := make_progress(20, 20, RED, Vector2(270, 14))
	sanity_bar.position = Vector2(16, 188)
	panel.add_child(sanity_bar)
	var ap_caption := make_label("本次行動  ·  AP", 12, MUTED)
	ap_caption.position = Vector2(16, 222)
	panel.add_child(ap_caption)
	var ap_row := HBoxContainer.new()
	ap_row.position = Vector2(16, 249)
	ap_row.add_theme_constant_override("separation", 8)
	panel.add_child(ap_row)
	for i in range(3):
		var pip := Panel.new()
		pip.custom_minimum_size = Vector2(82, 28)
		pip.add_theme_stylebox_override("panel", panel_style(Color(0.74, 0.49, 0.22, 0.92), 8, Color(0.95, 0.77, 0.43, 0.72), 1))
		var pip_text := make_label("可用", 12, Color("#fff1d0"))
		pip_text.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		pip_text.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
		pip.add_child(pip_text)
		ap_row.add_child(pip)
	var deck_caption := make_label("牌組", 12, MUTED)
	deck_caption.position = Vector2(16, 300)
	panel.add_child(deck_caption)
	var deck_value := make_label("30 張", 18, TEXT)
	deck_value.position = Vector2(72, 295)
	panel.add_child(deck_value)
	var discard_caption := make_label("棄牌  0", 12, MUTED)
	discard_caption.position = Vector2(180, 300)
	panel.add_child(discard_caption)

func _build_encounter_panel() -> void:
	var panel := add_panel(self, Vector2(354, 112), Vector2(546, 352), Color(0.04, 0.055, 0.06, 0.78), Color(0.72, 0.61, 0.42, 0.38), 22)
	var title := make_label("荒野邊界", 22, TEXT)
	title.position = Vector2(24, 18)
	panel.add_child(title)
	var tag := make_label("探索節點 01", 12, GOLD)
	tag.position = Vector2(410, 24)
	panel.add_child(tag)
	add_rule(panel, Vector2(24, 58), Vector2(498, 1), Color(0.75, 0.63, 0.42, 0.3))
	var portrait := TextureRect.new()
	portrait.position = Vector2(175, 78)
	portrait.size = Vector2(196, 242)
	portrait.texture = load("res://assets/characters/character-wanderer-v1-transparent.png")
	portrait.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	portrait.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	portrait.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	portrait.modulate = Color(1.0, 0.93, 0.78, 0.94)
	panel.add_child(portrait)
	var prompt := make_label("前方的風穿過枯木，\n遠處有火光回應。", 15, Color("#eee3d0"))
	prompt.position = Vector2(28, 276)
	prompt.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0.8))
	prompt.add_theme_constant_override("shadow_offset_x", 1)
	prompt.add_theme_constant_override("shadow_offset_y", 1)
	panel.add_child(prompt)
	var status := make_label("尚未進入遭遇  ·  點選手牌查看詳情", 12, MUTED)
	status.position = Vector2(28, 326)
	panel.add_child(status)

func _build_enemy_panel() -> void:
	var panel := add_panel(self, Vector2(924, 112), Vector2(328, 352), Color(0.045, 0.06, 0.064, 0.9), Color(0.68, 0.55, 0.37, 0.46), 18)
	var heading := make_label("遭遇資訊", 12, GOLD)
	heading.position = Vector2(20, 16)
	panel.add_child(heading)
	var enemy_name := make_label("荒野中的回聲", 22, TEXT)
	enemy_name.position = Vector2(20, 48)
	panel.add_child(enemy_name)
	var enemy_state := make_label("等待遭遇開始", 13, MUTED)
	enemy_state.position = Vector2(22, 82)
	panel.add_child(enemy_state)
	add_rule(panel, Vector2(20, 112), Vector2(288, 1), Color(0.73, 0.61, 0.42, 0.36))
	var intent_label := make_label("行動預覽", 12, MUTED)
	intent_label.position = Vector2(20, 130)
	panel.add_child(intent_label)
	var intents := HBoxContainer.new()
	intents.position = Vector2(20, 164)
	intents.add_theme_constant_override("separation", 8)
	panel.add_child(intents)
	for item in [["當前", "準備"], ["+1", "攻擊"], ["+2", "恢復"]]:
		var slot := Panel.new()
		slot.custom_minimum_size = Vector2(88, 82)
		slot.add_theme_stylebox_override("panel", panel_style(Color(0.11, 0.12, 0.12, 0.86), 12, Color(0.64, 0.55, 0.42, 0.34), 1))
		var slot_top := make_label(item[0], 11, GOLD)
		slot_top.position = Vector2(11, 10)
		slot.add_child(slot_top)
		var slot_name := make_label(item[1], 15, TEXT)
		slot_name.position = Vector2(11, 37)
		slot.add_child(slot_name)
		intents.add_child(slot)
	var note := make_label("你將看見當前與接下來兩次行動。", 12, MUTED)
	note.position = Vector2(20, 276)
	note.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	note.size = Vector2(270, 42)
	panel.add_child(note)

func _build_hand() -> void:
	var hand_panel := add_panel(self, Vector2(28, 486), Vector2(1224, 214), Color(0.035, 0.045, 0.05, 0.94), Color(0.78, 0.64, 0.39, 0.5), 20)
	var hand_title := make_label("你的手牌", 14, TEXT)
	hand_title.position = Vector2(20, 14)
	hand_panel.add_child(hand_title)
	var hand_meta := make_label("7 / 7  ·  點選一張查看牌面與效果", 12, MUTED)
	hand_meta.position = Vector2(132, 17)
	hand_panel.add_child(hand_meta)
	var action_hint := make_label("選擇牌後可加入本次行動", 12, GOLD)
	action_hint.position = Vector2(955, 17)
	hand_panel.add_child(action_hint)
	add_rule(hand_panel, Vector2(20, 43), Vector2(1180, 1), Color(0.72, 0.61, 0.43, 0.3))
	var hand := Control.new()
	hand.position = Vector2(18, 54)
	hand.size = Vector2(1188, 154)
	hand.mouse_filter = Control.MOUSE_FILTER_PASS
	hand_panel.add_child(hand)
	var overlap := 12.0
	var step := CARD_SIZE.x - overlap
	var total_width := CARD_SIZE.x + step * float(hand_cards.size() - 1)
	var start_x := (1188.0 - total_width) * 0.5
	for i in range(hand_cards.size()):
		var item: Array = hand_cards[i]
		var card := TextureButton.new()
		card.size = CARD_SIZE
		card.position = Vector2(start_x + step * float(i), 0)
		card.ignore_texture_size = true
		card.stretch_mode = TextureButton.STRETCH_KEEP_ASPECT_CENTERED
		card.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
		card.texture_normal = load(item[2])
		card.material = _rounded_card_material(0.065)
		card.tooltip_text = item[0] + "  " + item[1]
		hand.add_child(card)
		card_home[card] = card.position
		hand_cards_nodes.append(card)
		card.z_index = i
		card.mouse_entered.connect(func(c = card): _hover_card(c, true))
		card.mouse_exited.connect(func(c = card): _hover_card(c, false))
		card.pressed.connect(func(c = card, data = item): _select_card(c, data))

func _build_inspector() -> void:
	inspector = add_panel(self, Vector2(278, 92), Vector2(724, 370), Color(0.045, 0.055, 0.06, 0.98), Color(0.88, 0.69, 0.36, 0.7), 22)
	inspector.mouse_filter = Control.MOUSE_FILTER_IGNORE
	inspector.z_index = 50
	inspector.visible = false
	var close_hint := make_label("再次點選手牌或空白處收起詳情", 12, MUTED)
	close_hint.position = Vector2(438, 326)
	inspector.add_child(close_hint)
	inspector_card = TextureRect.new()
	inspector_card.position = Vector2(22, 18)
	inspector_card.size = Vector2(228, 332)
	inspector_card.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	inspector_card.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	inspector_card.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	inspector_card.mouse_filter = Control.MOUSE_FILTER_IGNORE
	inspector_card.material = _rounded_card_material(0.065)
	inspector.add_child(inspector_card)
	inspector_text = VBoxContainer.new()
	inspector_text.position = Vector2(286, 44)
	inspector_text.size = Vector2(390, 230)
	inspector_text.add_theme_constant_override("separation", 10)
	inspector_text.mouse_filter = Control.MOUSE_FILTER_IGNORE
	inspector.add_child(inspector_text)

func _hover_card(card: TextureButton, hovering: bool) -> void:
	if card == selected_card:
		return
	var home: Vector2 = card_home[card]
	if hovering:
		card.z_index = 500
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
		_tween_card(previous, card_home[previous], Vector2.ONE)
	selected_card = card
	card.z_index = 1000
	_tween_card(card, card_home[card] + Vector2(0, SELECT_Y), Vector2(1.03, 1.03))
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
		_tween_card(old, card_home[old], Vector2.ONE)
	inspector.visible = false

func _restore_card_layer(card: TextureButton) -> void:
	var index: int = hand_cards_nodes.find(card)
	card.z_index = index if index >= 0 else 0

func _tween_card(card: TextureButton, target_pos: Vector2, target_scale: Vector2) -> void:
	if card_tweens.has(card) and is_instance_valid(card_tweens[card]):
		card_tweens[card].kill()
	card.pivot_offset = CARD_SIZE * 0.5
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
