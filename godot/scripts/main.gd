extends Control

const CARD_SIZE := Vector2(130, 170)
const HOVER_Y := -12.0
const SELECT_Y := -44.0
const MOTION_TIME := 0.16

var hand_cards = [
	["群羊", "1", "res://assets/cards/sheep-1.png", "物資", "基礎數值 1。可與其他群羊牌組合，連續打出 3 張群羊可觸發【成群】。"],
	["糧食", "3", "res://assets/cards/food-3.png", "物資", "基礎數值 3。可與其他糧食牌組合，連續打出 3 張糧食可觸發【播種】。"],
	["金錢", "2", "res://assets/cards/money-2.png", "物資", "基礎數值 2。可與其他金錢牌組合，連續打出 3 張金錢可觸發【積累】。"],
	["恩典", "3", "res://assets/cards/grace-3.png", "特殊物資", "基礎數值 3。行動中包含恩典時，可觸發【恩賜】，額外成全一次可對應的法則。"],
	["群羊", "1", "res://assets/cards/sheep-1.png", "物資", "基礎數值 1。可與其他群羊牌組合，連續打出 3 張群羊可觸發【成群】。"],
	["糧食", "3", "res://assets/cards/food-3.png", "物資", "基礎數值 3。可與其他糧食牌組合，連續打出 3 張糧食可觸發【播種】。"],
	["金錢", "2", "res://assets/cards/money-2.png", "物資", "基礎數值 2。可與其他金錢牌組合，連續打出 3 張金錢可觸發【積累】。"],
]

var selected_card: TextureButton
var card_home := {}
var card_tweens := {}
var hand_cards_nodes: Array[TextureButton] = []
var inspector: Control
var inspector_card: TextureRect
var inspector_text: VBoxContainer

func _ready() -> void:
	_build_vertical_slice()
	print("Shepherd Reboot Godot card inspector prototype ready.")

func panel_style(color: Color, radius := 14, border := Color(0.7,0.58,0.36,0.5), width := 1) -> StyleBoxFlat:
	var s=StyleBoxFlat.new()
	s.bg_color=color
	s.corner_radius_top_left=radius; s.corner_radius_top_right=radius
	s.corner_radius_bottom_left=radius; s.corner_radius_bottom_right=radius
	s.border_width_left=width; s.border_width_top=width; s.border_width_right=width; s.border_width_bottom=width
	s.border_color=border
	return s

func make_label(text:String, font_px:int, color:=Color.WHITE) -> Label:
	var l=Label.new()
	l.text=text
	l.add_theme_font_size_override("font_size",font_px)
	l.add_theme_color_override("font_color",color)
	return l

func _build_vertical_slice() -> void:
	var bg=ColorRect.new(); bg.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT); bg.color=Color("#17130f"); add_child(bg)
	# Final wilderness awakening background.
	var scene_bg=TextureRect.new()
	scene_bg.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	scene_bg.texture=load("res://assets/backgrounds/wilderness-awakening.jpg")
	scene_bg.expand_mode=TextureRect.EXPAND_IGNORE_SIZE
	scene_bg.stretch_mode=TextureRect.STRETCH_KEEP_ASPECT_COVERED
	scene_bg.mouse_filter=Control.MOUSE_FILTER_IGNORE
	add_child(scene_bg)
	# Subtle cinematic grading: preserve the art while keeping HUD/cards readable.
	var top_shade=ColorRect.new(); top_shade.position=Vector2(0,0); top_shade.size=Vector2(1280,105); top_shade.color=Color(0.025,0.022,0.019,0.30); top_shade.mouse_filter=Control.MOUSE_FILTER_IGNORE; add_child(top_shade)
	var bottom_shade=ColorRect.new(); bottom_shade.position=Vector2(0,485); bottom_shade.size=Vector2(1280,235); bottom_shade.color=Color(0.018,0.016,0.014,0.55); bottom_shade.mouse_filter=Control.MOUSE_FILTER_IGNORE; add_child(bottom_shade)

	var top=HBoxContainer.new(); top.position=Vector2(22,18); top.size=Vector2(1236,52); top.add_theme_constant_override("separation",9); add_child(top)
	for datum in [["理智","8 / 10"],["行動","3 / 3"]]:
		var p=PanelContainer.new(); p.custom_minimum_size=Vector2(126,44); p.add_theme_stylebox_override("panel",panel_style(Color(0.035,0.031,0.027,0.68),12,Color(0.88,0.74,0.48,0.24),1))
		var row=HBoxContainer.new(); row.add_theme_constant_override("separation",8); p.add_child(row)
		var k=make_label(datum[0],12,Color("#c9b99d")); var v=make_label(datum[1],17,Color("#fff0d2")); row.add_child(k); row.add_child(v); top.add_child(p)
	var spacer=Control.new(); spacer.size_flags_horizontal=Control.SIZE_EXPAND_FILL; top.add_child(spacer)
	var chapter=VBoxContainer.new(); chapter.alignment=BoxContainer.ALIGNMENT_END
	var ch=make_label("序章",12,Color("#b9a98e")); ch.horizontal_alignment=HORIZONTAL_ALIGNMENT_RIGHT; chapter.add_child(ch)
	var loc=make_label("荒野甦醒",20,Color("#f2e2c3")); loc.horizontal_alignment=HORIZONTAL_ALIGNMENT_RIGHT; chapter.add_child(loc); top.add_child(chapter)


	# First-person exploration view: the protagonist is intentionally not pinned on-screen.


	var hand=Control.new(); hand.position=Vector2(0,526); hand.size=Vector2(1280,180); hand.mouse_filter=Control.MOUSE_FILTER_PASS; add_child(hand)
	var count: int = hand_cards.size()
	var overlap: float = 16.0
	var step: float = CARD_SIZE.x - overlap
	var total_width: float = CARD_SIZE.x + step * float(count - 1)
	var start_x: float = (1280.0 - total_width) * 0.5
	var center: float = float(count - 1) * 0.5
	for i in range(count):
		var item: Array = hand_cards[i]
		var card=TextureButton.new(); card.size=CARD_SIZE; card.position=Vector2(start_x + step * float(i), 0); card.ignore_texture_size=true; card.stretch_mode=TextureButton.STRETCH_KEEP_ASPECT_CENTERED; card.texture_normal=load(item[2]); card.material=_rounded_card_material(0.065); hand.add_child(card)
		card_home[card]=card.position
		hand_cards_nodes.append(card)
		card.z_index = i
		card.mouse_entered.connect(func(c=card): _hover_card(c, true))
		card.mouse_exited.connect(func(c=card): _hover_card(c, false))
		card.pressed.connect(func(c=card, data=item): _select_card(c, data))

	_build_inspector()
	var hand_hint=make_label("手牌",12,Color("#8f816d")); hand_hint.position=Vector2(24,684); add_child(hand_hint)

func _build_inspector() -> void:
	inspector=Control.new()
	inspector.position=Vector2(140,118)
	inspector.size=Vector2(690,350)
	inspector.mouse_filter=Control.MOUSE_FILTER_IGNORE
	inspector.z_index=50
	inspector.visible=false
	add_child(inspector)

	inspector_card=TextureRect.new()
	inspector_card.position=Vector2(0,18)
	inspector_card.size=Vector2(210,293)
	inspector_card.expand_mode=TextureRect.EXPAND_IGNORE_SIZE
	inspector_card.stretch_mode=TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	inspector_card.mouse_filter=Control.MOUSE_FILTER_IGNORE
	inspector.add_child(inspector_card)

	inspector_text=VBoxContainer.new()
	inspector_text.position=Vector2(238,62)
	inspector_text.size=Vector2(430,210)
	inspector_text.add_theme_constant_override("separation",7)
	inspector_text.mouse_filter=Control.MOUSE_FILTER_IGNORE
	inspector.add_child(inspector_text)

func _hover_card(card: TextureButton, hovering: bool) -> void:
	if card == selected_card:
		return
	var home: Vector2 = card_home[card]
	if hovering:
		card.z_index=500
		_tween_card(card, home + Vector2(0,HOVER_Y), Vector2(1.025,1.025))
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
	selected_card=card
	card.z_index=1000
	_tween_card(card, card_home[card] + Vector2(0,SELECT_Y), Vector2(1.06,1.06))
	_show_inspector(data)

func _show_inspector(data: Array) -> void:
	inspector_card.texture=load(data[2]); inspector_card.material=_rounded_card_material(0.065)
	for child in inspector_text.get_children():
		child.queue_free()
	var title=make_label(data[0] + "  ·  " + data[1],28,Color("#f4e2bd"))
	title.add_theme_color_override("font_shadow_color",Color(0,0,0,0.92)); title.add_theme_constant_override("shadow_offset_x",2); title.add_theme_constant_override("shadow_offset_y",2)
	inspector_text.add_child(title)
	var kind=make_label(data[3],14,Color("#d1b98f")); inspector_text.add_child(kind)
	var desc=make_label(data[4],17,Color("#f1eadc")); desc.autowrap_mode=TextServer.AUTOWRAP_WORD_SMART; desc.add_theme_color_override("font_shadow_color",Color(0,0,0,0.95)); desc.add_theme_constant_override("shadow_offset_x",2); desc.add_theme_constant_override("shadow_offset_y",2); inspector_text.add_child(desc)
	inspector.modulate.a=0.0
	inspector.position.y=126
	inspector.visible=true
	var tw=create_tween().set_parallel(true)
	tw.set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
	tw.tween_property(inspector,"modulate:a",1.0,0.14)
	tw.tween_property(inspector,"position:y",118.0,0.16)

func _close_inspector() -> void:
	if selected_card:
		var old=selected_card
		selected_card=null
		_restore_card_layer(old)
		_tween_card(old,card_home[old],Vector2.ONE)
	inspector.visible=false

func _restore_card_layer(card: TextureButton) -> void:
	var index: int = hand_cards_nodes.find(card)
	if index < 0:
		card.z_index=0
		return
	card.z_index=index

func _tween_card(card: TextureButton, target_pos: Vector2, target_scale: Vector2) -> void:
	if card_tweens.has(card) and is_instance_valid(card_tweens[card]):
		card_tweens[card].kill()
	card.pivot_offset=CARD_SIZE*0.5
	var tw=create_tween().set_parallel(true)
	tw.set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
	tw.tween_property(card,"position",target_pos,MOTION_TIME)
	tw.tween_property(card,"scale",target_scale,MOTION_TIME)
	card_tweens[card]=tw

func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and event.pressed and selected_card:
		var local_mouse=get_local_mouse_position()
		var card_rect=Rect2(selected_card.global_position,selected_card.size*selected_card.scale)
		var inspector_rect=Rect2(inspector.global_position,inspector.size)
		if not card_rect.has_point(local_mouse) and not inspector_rect.has_point(local_mouse):
			_close_inspector()



func _rounded_card_material(radius: float) -> ShaderMaterial:
	var shader := Shader.new()
	shader.code = """shader_type canvas_item;
render_mode unshaded;
uniform float radius = 0.065;
uniform float edge_softness = 0.008;

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
	material.set_shader_parameter("edge_softness", 0.006)
	return material
