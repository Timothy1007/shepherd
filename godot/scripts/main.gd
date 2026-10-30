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
	var bg=ColorRect.new(); bg.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT); bg.color=Color("#3b2d22"); add_child(bg)
	var sky=ColorRect.new(); sky.position=Vector2(0,0); sky.size=Vector2(1280,455); sky.color=Color("#78634a"); add_child(sky)
	var horizon=Polygon2D.new(); horizon.polygon=PackedVector2Array([Vector2(0,310),Vector2(150,220),Vector2(290,300),Vector2(450,190),Vector2(650,290),Vector2(830,210),Vector2(1030,285),Vector2(1180,205),Vector2(1280,250),Vector2(1280,520),Vector2(0,520)]); horizon.color=Color("#4b4034"); add_child(horizon)
	var ground=ColorRect.new(); ground.position=Vector2(0,430); ground.size=Vector2(1280,290); ground.color=Color("#251f1a"); add_child(ground)

	var top=HBoxContainer.new(); top.position=Vector2(24,20); top.size=Vector2(1232,58); top.add_theme_constant_override("separation",12); add_child(top)
	for t in ["理智  8/10","營火  6","行動點  3/3"]:
		var p=PanelContainer.new(); p.custom_minimum_size=Vector2(150,48); p.add_theme_stylebox_override("panel",panel_style(Color(0.07,0.06,0.05,0.86),12)); var x=make_label(t,18,Color("#f1dfbd")); x.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER; x.vertical_alignment=VERTICAL_ALIGNMENT_CENTER; p.add_child(x); top.add_child(p)
	var spacer=Control.new(); spacer.size_flags_horizontal=Control.SIZE_EXPAND_FILL; top.add_child(spacer)
	top.add_child(make_label("序章 · 荒野甦醒",18,Color("#e8d8b8")))

	var portrait=TextureRect.new(); portrait.texture=load("res://../docs/art/reference/character-wanderer-v1去背.png"); portrait.expand_mode=TextureRect.EXPAND_IGNORE_SIZE; portrait.stretch_mode=TextureRect.STRETCH_KEEP_ASPECT_CENTERED; portrait.position=Vector2(900,78); portrait.size=Vector2(330,370); portrait.texture_filter=CanvasItem.TEXTURE_FILTER_LINEAR; add_child(portrait)


	var hand=HBoxContainer.new(); hand.position=Vector2(65,525); hand.size=Vector2(1150,180); hand.alignment=BoxContainer.ALIGNMENT_CENTER; hand.add_theme_constant_override("separation",-14); add_child(hand)
	for item in hand_cards:
		var slot=Control.new(); slot.custom_minimum_size=CARD_SIZE
		var card=TextureButton.new(); card.size=CARD_SIZE; card.ignore_texture_size=true; card.stretch_mode=TextureButton.STRETCH_KEEP_ASPECT_CENTERED; card.texture_normal=_rounded_texture(item[2], CARD_SIZE, 26.0); slot.add_child(card); hand.add_child(slot)
		card_home[card]=Vector2.ZERO
		card.mouse_entered.connect(func(c=card): _hover_card(c, true))
		card.mouse_exited.connect(func(c=card): _hover_card(c, false))
		card.pressed.connect(func(c=card, data=item): _select_card(c, data))

	_build_inspector()
	var tag=make_label("第一人稱場景構圖測試",14,Color("#cbbda4")); tag.position=Vector2(28,690); add_child(tag)

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
	_tween_card(card, Vector2(0, HOVER_Y if hovering else 0), Vector2.ONE if not hovering else Vector2(1.025,1.025))

func _select_card(card: TextureButton, data: Array) -> void:
	if selected_card == card:
		_close_inspector()
		return
	if selected_card:
		_tween_card(selected_card, Vector2.ZERO, Vector2.ONE)
	selected_card=card
	card.z_index=20
	_tween_card(card, Vector2(0, SELECT_Y), Vector2(1.06,1.06))
	_show_inspector(data)

func _show_inspector(data: Array) -> void:
	inspector_card.texture=_rounded_texture(data[2], Vector2(210,293), 30.0)
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
		old.z_index=0
		_tween_card(old,Vector2.ZERO,Vector2.ONE)
	inspector.visible=false

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


func _rounded_texture(path: String, target_size: Vector2, radius: float) -> ImageTexture:
	var source: Texture2D = load(path)
	var image: Image = source.get_image()
	image.resize(int(target_size.x), int(target_size.y), Image.INTERPOLATE_LANCZOS)
	var w: int = image.get_width()
	var h: int = image.get_height()
	var r: int = int(radius)
	for y in range(h):
		for x in range(w):
			var dx: int = maxi(r - x, maxi(0, x - (w - 1 - r)))
			var dy: int = maxi(r - y, maxi(0, y - (h - 1 - r)))
			if dx > 0 and dy > 0 and dx * dx + dy * dy > r * r:
				var col: Color = image.get_pixel(x,y)
				col.a = 0.0
				image.set_pixel(x,y,col)
	return ImageTexture.create_from_image(image)
