extends Control

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
var card_info: VBoxContainer

func _ready() -> void:
	_build_vertical_slice()
	print("Shepherd Reboot Godot vertical slice v0.1 ready.")

func panel_style(color: Color, radius := 14, border := Color(0.7,0.58,0.36,0.5), width := 1) -> StyleBoxFlat:
	var s=StyleBoxFlat.new()
	s.bg_color=color
	s.corner_radius_top_left=radius; s.corner_radius_top_right=radius
	s.corner_radius_bottom_left=radius; s.corner_radius_bottom_right=radius
	s.border_width_left=width; s.border_width_top=width; s.border_width_right=width; s.border_width_bottom=width
	s.border_color=border
	return s

func label(text:String, size:int, color:=Color.WHITE) -> Label:
	var l=Label.new(); l.text=text; l.add_theme_font_size_override("font_size",size); l.add_theme_color_override("font_color",color)
	return l

func _build_vertical_slice() -> void:
	var bg=ColorRect.new(); bg.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT); bg.color=Color("#3b2d22"); add_child(bg)
	var sky=ColorRect.new(); sky.position=Vector2(0,0); sky.size=Vector2(1280,455); sky.color=Color("#78634a"); add_child(sky)
	var horizon=Polygon2D.new(); horizon.polygon=PackedVector2Array([Vector2(0,310),Vector2(150,220),Vector2(290,300),Vector2(450,190),Vector2(650,290),Vector2(830,210),Vector2(1030,285),Vector2(1180,205),Vector2(1280,250),Vector2(1280,520),Vector2(0,520)]); horizon.color=Color("#4b4034"); add_child(horizon)
	var ground=ColorRect.new(); ground.position=Vector2(0,430); ground.size=Vector2(1280,290); ground.color=Color("#251f1a"); add_child(ground)

	var top=HBoxContainer.new(); top.position=Vector2(24,20); top.size=Vector2(1232,58); top.add_theme_constant_override("separation",12); add_child(top)
	for t in ["理智  8/10","營火  6","行動點  3/3"]:
		var p=PanelContainer.new(); p.custom_minimum_size=Vector2(150,48); p.add_theme_stylebox_override("panel",panel_style(Color(0.07,0.06,0.05,0.86),12)); var x=label(t,18,Color("#f1dfbd")); x.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER; x.vertical_alignment=VERTICAL_ALIGNMENT_CENTER; p.add_child(x); top.add_child(p)
	var spacer=Control.new(); spacer.size_flags_horizontal=Control.SIZE_EXPAND_FILL; top.add_child(spacer)
	var chapter=label("序章 · 荒野甦醒",18,Color("#e8d8b8")); top.add_child(chapter)

	var portrait=TextureRect.new(); portrait.texture=load("res://assets/characters/character-wanderer-v1.png"); portrait.expand_mode=TextureRect.EXPAND_IGNORE_SIZE; portrait.stretch_mode=TextureRect.STRETCH_KEEP_ASPECT_CENTERED; portrait.position=Vector2(850,88); portrait.size=Vector2(390,410); portrait.modulate=Color(0.94,0.88,0.78,1); add_child(portrait)

	var dialog=PanelContainer.new(); dialog.position=Vector2(52,350); dialog.size=Vector2(710,150); dialog.add_theme_stylebox_override("panel",panel_style(Color(0.055,0.047,0.039,0.91),16,Color(0.78,0.64,0.4,0.7),1)); add_child(dialog)
	var dv=VBoxContainer.new(); dv.add_theme_constant_override("separation",7); dialog.add_child(dv)
	var who=label("陌生的老人",20,Color("#d7b778")); dv.add_child(who)
	var line=label("「你醒了。這片荒野不會等人——如果還走得動，就跟上。」",19,Color("#f1eadc")); line.autowrap_mode=TextServer.AUTOWRAP_WORD_SMART; dv.add_child(line)
	var hint=label("點擊手牌查看 · 此畫面為 Godot v0.1 視覺切片",13,Color("#9d9589")); dv.add_child(hint)

	card_info=VBoxContainer.new(); card_info.position=Vector2(190,430); card_info.size=Vector2(520,92); card_info.add_theme_constant_override("separation",2); card_info.mouse_filter=Control.MOUSE_FILTER_IGNORE; card_info.visible=false; add_child(card_info)

	var hand=HBoxContainer.new(); hand.position=Vector2(65,525); hand.size=Vector2(1150,180); hand.alignment=BoxContainer.ALIGNMENT_CENTER; hand.add_theme_constant_override("separation",-14); add_child(hand)
	for item in hand_cards:
		var card=TextureButton.new(); card.custom_minimum_size=Vector2(130,170); card.ignore_texture_size=true; card.stretch_mode=TextureButton.STRETCH_KEEP_ASPECT_CENTERED; card.texture_normal=load(item[2]); card.tooltip_text=item[0]; card.mouse_entered.connect(func(c=card): if_selected_hover(c, true)); card.mouse_exited.connect(func(c=card): if_selected_hover(c, false)); card.pressed.connect(func(c=card, data=item): select_card(c, data)); hand.add_child(card)

	var tag=label("第一人稱場景構圖測試",14,Color("#cbbda4")); tag.position=Vector2(28,690); add_child(tag)

func if_selected_hover(card: TextureButton, hovering: bool) -> void:
	if card == selected_card:
		return
	card.position.y = -12 if hovering else 0

func select_card(card: TextureButton, data: Array) -> void:
	if selected_card == card:
		selected_card.position.y=0
		selected_card=null
		card_info.visible=false
		return
	if selected_card:
		selected_card.position.y=0
	selected_card=card
	card.position.y=-42
	for child in card_info.get_children():
		child.queue_free()
	var title=label(data[0] + "  ·  " + data[1],22,Color("#f4e2bd")); title.add_theme_color_override("font_shadow_color",Color(0,0,0,0.9)); title.add_theme_constant_override("shadow_offset_x",2); title.add_theme_constant_override("shadow_offset_y",2); card_info.add_child(title)
	var kind=label(data[3],13,Color("#c7b28e")); kind.add_theme_color_override("font_shadow_color",Color(0,0,0,0.9)); kind.add_theme_constant_override("shadow_offset_x",1); kind.add_theme_constant_override("shadow_offset_y",1); card_info.add_child(kind)
	var desc=label(data[4],15,Color("#f1eadc")); desc.autowrap_mode=TextServer.AUTOWRAP_WORD_SMART; desc.add_theme_color_override("font_shadow_color",Color(0,0,0,0.95)); desc.add_theme_constant_override("shadow_offset_x",2); desc.add_theme_constant_override("shadow_offset_y",2); card_info.add_child(desc)
	card_info.visible=true
