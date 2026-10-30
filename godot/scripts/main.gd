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
	_build_encounter_panel()
	_build_enemy_panel()
	_build_hand()
	_build_inspector()

func _build_header() -> void:
	var header := Control.new()
	header.position = Vector2(28, 20)
	header.size = Vector2(1224, 66)
	header.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(header)
	var eyebrow := make_label("REBOOT  ·  第一章／荒野邊界", 11, GOLD)
	eyebrow.position = Vector2(0, 0)
	header.add_child(eyebrow)
	var title := make_label("牧羊人：荒野之歌", 28, TEXT)
	title.position = Vector2(0, 16)
	header.add_child(title)
	add_rule(header, Vector2(310, 18), Vector2(1, 30), Color(0.78, 0.67, 0.48, 0.50))
	var state := make_label("遭遇準備中", 13, GOLD)
	state.position = Vector2(338, 25)
	header.add_child(state)
	var nav := HBoxContainer.new()
	nav.position = Vector2(930, 8)
	nav.size = Vector2(292, 42)
	nav.alignment = BoxContainer.ALIGNMENT_END
	nav.add_theme_constant_override("separation", 3)
	header.add_child(nav)
	for item in ["地圖", "探索", "設定"]:
		nav.add_child(make_nav_button(item))

func _build_player_panel() -> void:
	var panel := Control.new()
	panel.position = Vector2(30, 132)
	panel.size = Vector2(270, 320)
	add_child(panel)
	var overline := make_label("行者狀態", 11, GOLD)
	overline.position = Vector2(0, 0)
	panel.add_child(overline)
	var portrait := TextureRect.new()
	portrait.position = Vector2(0, 30)
	portrait.size = Vector2(88, 120)
	portrait.texture = load("res://assets/characters/character-wanderer-v1.png")
	portrait.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	portrait.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	portrait.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	panel.add_child(portrait)
	var name := make_label("行者", 25, TEXT)
	name.position = Vector2(110, 34)
	panel.add_child(name)
	var role := make_label("在荒野中尋路的人", 13, MUTED)
	role.position = Vector2(112, 70)
	panel.add_child(role)
	add_rule(panel, Vector2(110, 101), Vector2(142, 1), Color(0.73, 0.61, 0.42, 0.40))
	var sanity_caption := make_label("理智", 12, MUTED)
	sanity_caption.position = Vector2(110, 116)
	panel.add_child(sanity_caption)
	var sanity_value := make_label("20 / 20", 18, TEXT)
	sanity_value.position = Vector2(184, 111)
	panel.add_child(sanity_value)
	var sanity_bar := make_progress(20, 20, RED, Vector2(270, 10))
	sanity_bar.position = Vector2(0, 164)
	panel.add_child(sanity_bar)
	var ap_caption := make_label("本次行動  ·  AP", 12, MUTED)
	ap_caption.position = Vector2(0, 190)
	panel.add_child(ap_caption)
	var ap_row := HBoxContainer.new()
	ap_row.position = Vector2(0, 218)
	ap_row.add_theme_constant_override("separation", 6)
	panel.add_child(ap_row)
	for i in range(3):
		var pip := Panel.new()
		pip.custom_minimum_size = Vector2(86, 22)
		pip.add_theme_stylebox_override("panel", panel_style(Color(0.74, 0.49, 0.22, 0.76), 7, Color(0.95, 0.77, 0.43, 0.62), 1))
		var pip_text := make_label("可用", 11, Color("#fff1d0"))
		pip_text.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		pip_text.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
		pip.add_child(pip_text)
		ap_row.add_child(pip)
	var deck_caption := make_label("牌組  30 張", 12, MUTED)
	deck_caption.position = Vector2(0, 266)
	panel.add_child(deck_caption)
	var discard_caption := make_label("棄牌  0", 12, MUTED)
	discard_caption.position = Vector2(170, 266)
	panel.add_child(discard_caption)

func _build_encounter_panel() -> void:
	var panel := Control.new()
	panel.position = Vector2(350, 116)
	panel.size = Vector2(560, 350)
	add_child(panel)
	var title := make_label("荒野邊界", 22, TEXT)
	title.position = Vector2(18, 0)
	panel.add_child(title)
	var tag := make_label("探索節點 01", 11, GOLD)
	tag.position = Vector2(436, 7)
	panel.add_child(tag)
	add_rule(panel, Vector2(18, 38), Vector2(500, 1), Color(0.75, 0.63, 0.42, 0.28))
	var portrait := TextureRect.new()
	portrait.position = Vector2(180, 48)
	portrait.size = Vector2(198, 242)
	portrait.texture = load("res://assets/characters/character-wanderer-v1-transparent.png")
	portrait.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	portrait.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	portrait.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	portrait.modulate = Color(1.0, 0.94, 0.82, 0.97)
	panel.add_child(portrait)
	var prompt := make_label("前方的風穿過枯木，\n遠處有火光回應。", 15, Color("#eee3d0"))
	prompt.position = Vector2(22, 286)
	prompt.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0.9))
	prompt.add_theme_constant_override("shadow_offset_x", 1)
	prompt.add_theme_constant_override("shadow_offset_y", 1)
	panel.add_child(prompt)
	var status := make_label("尚未進入遭遇  ·  點選手牌查看詳情", 12, MUTED)
	status.position = Vector2(22, 333)
	panel.add_child(status)

func _build_enemy_panel() -> void:
	var panel := Control.new()
	panel.position = Vector2(950, 132)
	panel.size = Vector2(282, 320)
	add_child(panel)
	var heading := make_label("遭遇資訊", 11, GOLD)
	heading.position = Vector2(0, 0)
	panel.add_child(heading)
	var enemy_name := make_label("荒野中的回聲", 21, TEXT)
	enemy_name.position = Vector2(0, 30)
	panel.add_child(enemy_name)
	var enemy_state := make_label("等待遭遇開始", 13, MUTED)
	enemy_state.position = Vector2(2, 65)
	panel.add_child(enemy_state)
	add_rule(panel, Vector2(0, 95), Vector2(270, 1), Color(0.73, 0.61, 0.42, 0.30))
	var intent_label := make_label("行動預覽", 12, MUTED)
	intent_label.position = Vector2(0, 114)
	panel.add_child(intent_label)
	var intents := HBoxContainer.new()
	intents.position = Vector2(0, 146)
	intents.add_theme_constant_override("separation", 7)
	panel.add_child(intents)
	for item in [["當前", "準備"], ["+1", "攻擊"], ["+2", "恢復"]]:
		var slot := Panel.new()
		slot.custom_minimum_size = Vector2(84, 70)
		slot.add_theme_stylebox_override("panel", panel_style(Color(0.04, 0.05, 0.05, 0.46), 10, Color(0.76, 0.64, 0.44, 0.30), 1))
		var slot_top := make_label(item[0], 10, GOLD)
		slot_top.position = Vector2(10, 8)
		slot.add_child(slot_top)
		var slot_name := make_label(item[1], 14, TEXT)
		slot_name.position = Vector2(10, 34)
		slot.add_child(slot_name)
		intents.add_child(slot)
	var note := make_label("當前與接下來兩次行動會提前顯示。", 12, MUTED)
	note.position = Vector2(0, 242)
	note.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	note.size = Vector2(260, 42)
	panel.add_child(note)

func _build_hand() -> void:
	var hand_panel := Control.new()
	hand_panel.position = Vector2(0, 500)
	hand_panel.size = Vector2(1280, 220)
	hand_panel.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(hand_panel)
	var shade := ColorRect.new()
	shade.position = Vector2(0, 26)
	shade.size = Vector2(1280, 194)
	shade.color = Color(0.025, 0.03, 0.03, 0.58)
	shade.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hand_panel.add_child(shade)
	var hand_title := make_label("你的手牌", 13, TEXT)
	hand_title.position = Vector2(42, 34)
	hand_panel.add_child(hand_title)
	var hand_meta := make_label("7 / 7  ·  點選牌面查看效果", 12, MUTED)
	hand_meta.position = Vector2(108, 36)
	hand_panel.add_child(hand_meta)
	var hand := Control.new()
	hand.position = Vector2(0, 58)
	hand.size = Vector2(1280, 162)
	hand.mouse_filter = Control.MOUSE_FILTER_PASS
	hand_panel.add_child(hand)
	var overlap := 28.0
	var step := CARD_SIZE.x - overlap
	var total_width := CARD_SIZE.x + step * float(hand_cards.size() - 1)
	var start_x := (1280.0 - total_width) * 0.5
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
	_tween_card(card, card_home[card] + Vector2(0, SELECT_Y), Vector2(1.08, 1.08))
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
