const cards=[
{id:'sheep',type:'物資',name:'群羊',number:1,tags:'群羊',effect:'同類物資：數字須等於或大於上一張。\n不同類物資：群羊克制糧食，可無視數字大小。'},
{id:'food',type:'物資',name:'糧食',number:1,tags:'糧食',effect:'同類物資：數字須等於或大於上一張。\n不同類物資：糧食克制金錢，可無視數字大小。'},
{id:'money',type:'物資',name:'金錢',number:1,tags:'金錢',effect:'同類物資：數字須等於或大於上一張。\n不同類物資：金錢克制群羊，可無視數字大小。'},
{id:'grace',type:'物資',name:'恩典',number:1,tags:'指定物資類型',effect:'出牌時指定為群羊、糧食或金錢，保留此牌的數字，依指定類型進行出牌判定與相關效果計算。'},
{id:'miracle',type:'神蹟',name:'回轉歸向',tags:'新生',summary:'改變出牌方向\n然後抽1張牌',effect:'改變出牌方向，然後你抽1張牌。'},
{id:'disaster',type:'災難',name:'方舟之外',tags:'群體 · 掠奪',summary:'各保留一種物資\n棄置其他物資\n至多2張',effect:'每名玩家選擇自己手中的1種物資種類並保留該種類的物資牌，然後從其他種類的物資牌中棄置2張；若不足2張，則全部棄置。'},
{id:'blessing',type:'祝福',name:'破土與新生',tags:'使用後棄置',summary:'抽1張牌\n然後棄置此祝福',price:10,effect:'使用後，你抽1張牌，然後棄置此祝福。'},
{id:'judgment',type:'審判',name:'失序',tags:'本輪生效',summary:'克制關係顛倒\n數字大小顛倒',effect:'本輪克制關係顛倒（群羊＜糧食＜金錢＜群羊）；數字大小也顛倒（1＞2＞3…）。'}
];
const hand=document.getElementById('hand');let selected='miracle';
function cardElement(c){const root=document.createElement('div');root.className='card '+c.id;const img=document.createElement('img');img.src='assets/'+c.id+'.png';img.alt='';img.draggable=false;root.append(img);if(c.number){for(const cl of ['number','number bottom']){const el=document.createElement('span');el.className=cl;el.textContent=c.number;root.append(el)}}else{const title=document.createElement('span');title.className='card-title';title.textContent=c.name;const summary=document.createElement('span');summary.className='card-summary';summary.textContent=c.summary;root.append(title,summary);if(c.price){const p=document.createElement('span');p.className='card-price';p.textContent=c.price+'點火種';root.append(p)}}return root}
function originalCard(c){const root=document.createElement('div');root.className='card original';const img=document.createElement('img');img.src='assets/'+c.id+'-original.png';img.alt=c.name+'：你提供的完整範例卡面';img.draggable=false;root.append(img);return root}
function selectCard(id){const c=cards.find(x=>x.id===id);selected=id;document.getElementById('large-card').replaceChildren(originalCard(c));document.getElementById('detail-type').textContent=c.type;document.getElementById('detail-name').textContent=c.name+(c.number?' '+c.number:'');document.getElementById('detail-tags').textContent=c.tags;document.getElementById('detail-effect').textContent=c.effect;document.getElementById('detail-price').textContent=c.price?'購買價格　'+c.price+'點火種':'';document.getElementById('selection-status').textContent='已選取：'+c.name;document.querySelectorAll('.hand-card').forEach(b=>b.setAttribute('aria-pressed',String(b.dataset.id===id)))}
cards.forEach((c,i)=>{const b=document.createElement('button');b.type='button';b.className='hand-card';b.dataset.id=c.id;b.style.setProperty('--tilt',(i-3.5)*1.4+'deg');b.style.setProperty('--arc',Math.abs(i-3.5)*2+'px');b.setAttribute('aria-label','查看'+c.type+'：'+c.name+(c.number?c.number:''));b.append(cardElement(c));b.addEventListener('click',()=>selectCard(c.id));b.addEventListener('keydown',e=>{if(e.key==='ArrowRight'||e.key==='ArrowLeft'){e.preventDefault();const j=(i+(e.key==='ArrowRight'?1:-1)+cards.length)%cards.length;hand.children[j].focus();selectCard(cards[j].id)}});hand.append(b)});selectCard(selected);
document.getElementById('back-to-hand').addEventListener('click',()=>document.querySelector('.hand-card[data-id="'+selected+'"]').focus());
let fontRequest=0;
async function setFont(){const request=++fontRequest;const f=document.getElementById('font').value;document.documentElement.style.setProperty('--title-font','"'+f+'"');const s=document.getElementById('font-status');s.textContent='字體載入中';try{const loaded=await Promise.race([document.fonts.load('20px "'+f+'"','回轉歸向方舟之外'),new Promise((_,reject)=>setTimeout(()=>reject(new Error('timeout')),10000))]);if(request===fontRequest)s.textContent=loaded.length?'字體已載入':'暫用系統字體'}catch(e){if(request===fontRequest)s.textContent='暫用系統字體'}}
document.getElementById('font').addEventListener('change',setFont);setFont();

document.getElementById('hand-mode').addEventListener('change',e=>{hand.classList.toggle('names-only',e.target.value==='names');});
