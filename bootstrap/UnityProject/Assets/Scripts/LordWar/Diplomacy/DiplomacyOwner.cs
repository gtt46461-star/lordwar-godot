using System;
using System.Collections.Generic;
namespace LordWar.Diplomacy {
    public sealed class DiplomacyOwner {
        readonly Dictionary<string,DiplomacyRelation> _relations=new Dictionary<string,DiplomacyRelation>();
        string Key(string a,string b){return string.CompareOrdinal(a,b)<0?a+"|"+b:b+"|"+a;}
        public DiplomacyRelation Get(string a,string b){string k=Key(a,b);DiplomacyRelation r;if(!_relations.TryGetValue(k,out r)){r=new DiplomacyRelation{A=a,B=b};_relations[k]=r;}return r;}
        public bool DeclareWar(string a,string b,int day){var r=Get(a,b);if(r.State==DiplomacyState.Vassal||r.TruceUntilDay>day||r.State==DiplomacyState.Alliance)return false;r.State=DiplomacyState.War;r.Opinion=Math.Min(r.Opinion,-60);r.CommonWar=false;r.SuzerainId="";r.VassalId="";return true;}
        public bool MakeTruce(string a,string b,int day,int duration){var r=Get(a,b);if(r.State!=DiplomacyState.War)return false;r.State=DiplomacyState.Neutral;r.TruceUntilDay=day+Math.Max(1,duration);r.Opinion=Math.Min(100,r.Opinion+6);r.CommonWar=false;return true;}
        public bool Trade(string a,string b){var r=Get(a,b);if(r.State==DiplomacyState.War||r.State==DiplomacyState.Vassal||r.Opinion<0)return false;r.State=DiplomacyState.Trade;r.Opinion=Math.Min(100,r.Opinion+4);return true;}
        public bool NonAggression(string a,string b,int day,int duration){var r=Get(a,b);if(r.State==DiplomacyState.War||r.State==DiplomacyState.Vassal||r.Opinion<10)return false;r.State=DiplomacyState.NonAggression;r.TruceUntilDay=Math.Max(r.TruceUntilDay,day+Math.Max(1,duration));r.Opinion=Math.Min(100,r.Opinion+3);return true;}
        public bool Alliance(string a,string b){var r=Get(a,b);if(r.State==DiplomacyState.War||r.State==DiplomacyState.Vassal||r.Opinion<20)return false;r.State=DiplomacyState.Alliance;r.Opinion=Math.Min(100,r.Opinion+5);return true;}
        public bool SetVassal(string suzerainId,string vassalId){if(string.IsNullOrEmpty(suzerainId)||string.IsNullOrEmpty(vassalId)||suzerainId==vassalId)return false;var r=Get(suzerainId,vassalId);r.State=DiplomacyState.Vassal;r.SuzerainId=suzerainId;r.VassalId=vassalId;r.CommonWar=true;r.TruceUntilDay=0;r.Opinion=Math.Max(r.Opinion,15);return true;}
        public bool IsVassalOf(string vassalId,string suzerainId){var r=Get(vassalId,suzerainId);return r.State==DiplomacyState.Vassal&&r.VassalId==vassalId&&r.SuzerainId==suzerainId;}
        public bool JoinCommonWar(string participantId,string enemyId){if(string.IsNullOrEmpty(participantId)||string.IsNullOrEmpty(enemyId)||participantId==enemyId)return false;var r=Get(participantId,enemyId);if(r.State==DiplomacyState.Vassal||r.State==DiplomacyState.Alliance)return false;r.State=DiplomacyState.War;r.CommonWar=true;r.Opinion=Math.Min(r.Opinion,-35);r.TruceUntilDay=0;return true;}
        public bool EndCommonWar(string participantId,string enemyId,int day){var r=Get(participantId,enemyId);if(r.State!=DiplomacyState.War||!r.CommonWar)return false;r.State=DiplomacyState.Neutral;r.CommonWar=false;r.TruceUntilDay=Math.Max(r.TruceUntilDay,day+7);r.Opinion=Math.Min(100,r.Opinion+2);return true;}
        public void ClearVassal(string a,string b){var r=Get(a,b);if(r.State!=DiplomacyState.Vassal)return;r.State=DiplomacyState.Neutral;r.SuzerainId="";r.VassalId="";r.CommonWar=false;}
        public void ChangeOpinion(string a,string b,int delta){var r=Get(a,b);r.Opinion=Mathx.Clamp(r.Opinion+delta,-100,100);}
        public int TradePartnerCount(string kingdomId){int n=0;foreach(DiplomacyRelation r in _relations.Values)if((r.A==kingdomId||r.B==kingdomId)&&(r.State==DiplomacyState.Trade||r.State==DiplomacyState.Alliance||r.State==DiplomacyState.Vassal))n++;return n;}
        public IEnumerable<DiplomacyRelation> All(){return _relations.Values;}
        public void Restore(IEnumerable<DiplomacyRelation> source){_relations.Clear();if(source==null)return;foreach(var r in source){if(r==null)continue;_relations[Key(r.A,r.B)]=r;}}
    }
}
