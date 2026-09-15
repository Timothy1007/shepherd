export const THRESHOLD_FIRE='fire';
export const THRESHOLD_HAND='hand';
export function thresholdCrossings(before,after){const out=[];if((before?.fire??0)>0&&(after?.fire??0)===0)out.push(THRESHOLD_FIRE);if((before?.handCount??0)>0&&(after?.handCount??0)===0)out.push(THRESHOLD_HAND);return out;}
export function canOvercomeDeath(before,after){return thresholdCrossings(before,after).length>0;}
export function applyOvercomeDeath({before,after,choice,restoreCard=null}){const crossings=thresholdCrossings(before,after);if(!crossings.includes(choice))return{ok:false,reason:'Selected value did not cross from above zero to zero.'};const next=structuredClone(after);if(choice===THRESHOLD_FIRE)next.fire=1;else{if(!restoreCard)return{ok:false,reason:'A hand card must be selected to remain.'};next.hand=[structuredClone(restoreCard)];}return{ok:true,value:next,crossings};}
