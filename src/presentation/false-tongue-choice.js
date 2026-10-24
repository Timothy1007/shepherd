import {GameController} from '../controller/game-controller.js';
import {getNormalActions} from '../game/game.js';
import {renderOptions} from './player-choice-row.js';
const previousPrepare=GameController.prototype.prepare;
let panel;
GameController.prototype.prepare=function(){
  const result=previousPrepare.call(this),window=this.state?.pendingFalseTongue?.window;
  const actorId=window?.stage==='offer'?window.responderPlayerId:window?.effect.playerId;
  const actor=this.state?.players.find(player=>player.playerId===actorId);
  if(!window||actor?.type!=='human'){if(panel)panel.hidden=true;return result;}
  const actions=getNormalActions(this.state).filter(action=>action.type==='resolveFalseTongue');
  if(!actions.length){if(panel)panel.hidden=true;return result;}
  if(!panel){panel=document.createElement('section');panel.id='false-tongue-choice';panel.hidden=true;document.body.append(panel);}
  const labels={pass:'不使用應對',use:'使用虛謊之舌',draw:'抽牌減少 1 張',fire:'火種減少 5 點',mission:'使命進度減少 2'};
  const options=actions.map(action=>({label:labels[action.choice]??action.choice,onSelect:()=>{panel.hidden=true;this.act(action,this.snapshot());}}));
  renderOptions(panel,'虛謊之舌',window.stage==='offer'?'有玩家即將因神蹟獲得效果，是否使用應對？':'選擇一項效果減少。',options);
  return result;
};
