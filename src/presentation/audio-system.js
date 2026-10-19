import { GameController } from '../controller/game-controller.js';
import { getDefinition } from '../game/cards.js';

const AUDIO_BASE='assets/audio/';
const FILES=Object.freeze({
  bgm:'bgm-wilderness.mp3',
  resource:'play-card.mp3',
  miracle:'miracle.mp3',
  disaster:'disaster.mp3',
  roundEnd:'round-end.mp3',
  fire:'fire-gain.mp3',
});
const STORAGE_KEY='shepherd-audio-v2';
let settings={music:true,sfx:true,musicVolume:.18,sfxVolume:.72};
try{settings={...settings,...JSON.parse(localStorage.getItem(STORAGE_KEY)||'{}')}}catch{}
settings.musicVolume=Math.max(0,Math.min(.22,Number(settings.musicVolume)||.18));
settings.sfxVolume=Math.max(0,Math.min(1,Number(settings.sfxVolume)||.72));
let unlocked=false,lastPlayedId=null,lastRound=null,lastFireTotal=null;
const bgm=new Audio(`${AUDIO_BASE}${FILES.bgm}`);bgm.loop=true;bgm.preload='auto';bgm.volume=settings.musicVolume;

function save(){localStorage.setItem(STORAGE_KEY,JSON.stringify(settings));}
function safePlay(audio){const promise=audio.play();if(promise?.catch)promise.catch(()=>{});}
function startMusic(){if(!unlocked||!settings.music)return;bgm.volume=settings.musicVolume;if(bgm.paused)safePlay(bgm);}
function stopMusic(){bgm.pause();}
function playSfx(key,{volume=1}={}){if(!unlocked||!settings.sfx||!FILES[key])return;const audio=new Audio(`${AUDIO_BASE}${FILES[key]}`);audio.volume=Math.max(0,Math.min(1,settings.sfxVolume*volume));safePlay(audio);}
function unlock(){if(unlocked)return;unlocked=true;startMusic();}

function ensureControls(){if(document.querySelector('#audio-controls'))return;const style=document.createElement('style');style.textContent=`#audio-controls{position:fixed;right:18px;top:18px;z-index:20500;display:flex;flex-wrap:wrap;gap:7px;padding:7px;border:1px solid #d7bd7a44;border-radius:12px;background:#07100dda;backdrop-filter:blur(8px);box-shadow:0 8px 24px #0007}#audio-controls button{border:1px solid #d7bd7a44;border-radius:9px;background:#ffffff0c;color:#eee8d8;padding:7px 10px;cursor:pointer;font:600 12px/1 system-ui}#audio-controls button[aria-pressed="false"]{opacity:.55}#audio-controls button:hover{background:#ffffff18}#deck-summary{flex:0 0 100%;padding-top:2px;color:#eee8d8;text-align:center;font:700 11px/1.35 system-ui;letter-spacing:.05em;text-shadow:0 1px 3px #000}#deck-summary strong{color:#f0c56f;font-size:12px}`;document.head.append(style);const host=document.createElement('div');host.id='audio-controls';host.setAttribute('aria-label','音訊控制與牌堆數量');host.innerHTML='<button id="music-toggle" type="button"></button><button id="sfx-toggle" type="button"></button><div id="deck-summary">牌庫 <strong>0</strong> 張 · 棄牌 <strong>0</strong> 張</div>';document.body.append(host);const music=host.querySelector('#music-toggle'),sfx=host.querySelector('#sfx-toggle');const paint=()=>{music.textContent=settings.music?'♫ 音樂':'♫ 靜音';music.setAttribute('aria-pressed',String(settings.music));sfx.textContent=settings.sfx?'✦ 音效':'✦ 靜音';sfx.setAttribute('aria-pressed',String(settings.sfx));};music.onclick=()=>{unlock();settings.music=!settings.music;settings.music?startMusic():stopMusic();save();paint();};sfx.onclick=()=>{unlock();settings.sfx=!settings.sfx;if(settings.sfx)playSfx('resource',{volume:.55});save();paint();};paint();}

function observeState(state){if(!state)return;const deck=state.deck?.filter(card=>card.definitionId!=='death').length??0,discard=state.discardPile?.length??0,summary=document.querySelector('#deck-summary');if(summary)summary.innerHTML=`牌庫 <strong>${deck}</strong> 張 · 棄牌 <strong>${discard}</strong> 張`;const played=state.playedArea?.at(-1);if(played&&played.instanceId!==lastPlayedId){lastPlayedId=played.instanceId;const d=getDefinition(played);playSfx(d.kind==='miracle'?'miracle':d.kind==='disaster'?'disaster':'resource');}
const round=state.round;if(lastRound!==null&&round!==lastRound)playSfx('roundEnd');lastRound=round;
const fireTotal=state.players?.reduce((sum,p)=>sum+(p.fire||0),0)??0;if(lastFireTotal!==null&&fireTotal>lastFireTotal)playSfx('fire',{volume:.65});lastFireTotal=fireTotal;}

const previousSafeRender=GameController.prototype.safeRender;
GameController.prototype.safeRender=function audioAwareRender(available){const result=previousSafeRender.call(this,available);try{observeState(this.state);}catch(error){console.warn('[Shepherd] audio state observer failed.',error);}return result;};

document.addEventListener('pointerdown',unlock,{once:true,capture:true});
document.addEventListener('keydown',unlock,{once:true,capture:true});
ensureControls();
observeState(globalThis.__shepherdController?.state);

window.ShepherdAudio=Object.freeze({
  playSfx,
  startMusic,
  stopMusic,
  get settings(){return{...settings};},
});
