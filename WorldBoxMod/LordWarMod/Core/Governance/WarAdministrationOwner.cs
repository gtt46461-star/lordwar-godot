using System;
using System.Collections.Generic;
using LordWar.Data;

namespace LordWar.Governance {
    /// <summary>战争行政唯一Owner：每场对外战争由一名现任官员负责国家层面的守城、野战、攻城、拦截和后勤协调；不替代将军的战术指挥。</summary>
    public sealed class WarAdministrationOwner {
        readonly Dictionary<string,WarAdministration> _wars = new Dictionary<string,WarAdministration>();
        readonly Dictionary<string,Person> _people;
        readonly Dictionary<string,Kingdom> _kingdoms;
        readonly GameDataCatalog _data;

        public WarAdministrationOwner(Dictionary<string,Person> people,Dictionary<string,Kingdom> kingdoms,GameDataCatalog data){_people=people;_kingdoms=kingdoms;_data=data;}
        static string Key(string kingdomId,string enemyKingdomId){return (kingdomId??"")+">"+(enemyKingdomId??"");}
        public IEnumerable<WarAdministration> All(){return _wars.Values;}
        public void Restore(IEnumerable<WarAdministration> rows){_wars.Clear();if(rows==null)return;foreach(WarAdministration x in rows){if(x==null||string.IsNullOrEmpty(x.Id)||string.IsNullOrEmpty(x.KingdomId)||string.IsNullOrEmpty(x.EnemyKingdomId))continue;Normalize(x);_wars[Key(x.KingdomId,x.EnemyKingdomId)]=x;}}
        public WarAdministration Get(string kingdomId,string enemyKingdomId){WarAdministration x;return _wars.TryGetValue(Key(kingdomId,enemyKingdomId),out x)?x:null;}
        public bool IsOfficialValid(string kingdomId,string officialId){Person p;return !string.IsNullOrEmpty(kingdomId)&&!string.IsNullOrEmpty(officialId)&&_people.TryGetValue(officialId,out p)&&p.Alive&&p.Injury!=InjuryState.Captured&&p.KingdomId==kingdomId&&p.Class==SocialClass.Official&&p.Job==JobKind.Official;}
        public bool OfficialBusyElsewhere(string kingdomId,string officialId,string exceptEnemyId=""){foreach(WarAdministration x in _wars.Values)if(x.KingdomId==kingdomId&&x.OfficialId==officialId&&x.EnemyKingdomId!=exceptEnemyId)return true;return false;}
        public WarAdministration Assign(string kingdomId,string enemyKingdomId,string officialId,int day,WarAdministrationFocus focus,out string reason){
            reason="";if(!_kingdoms.ContainsKey(kingdomId)||!_kingdoms.ContainsKey(enemyKingdomId)||kingdomId==enemyKingdomId){reason="战争双方无效";return null;}
            if(!IsOfficialValid(kingdomId,officialId)){reason="只能指派本国仍在任、未被俘的正式官员负责战争";return null;}
            if(OfficialBusyElsewhere(kingdomId,officialId,enemyKingdomId)){reason="该官员已经负责另一场战争，请先撤销原战务";return null;}
            string key=Key(kingdomId,enemyKingdomId);WarAdministration x;if(!_wars.TryGetValue(key,out x)){x=new WarAdministration{Id=Ids.Next("WARADM"),KingdomId=kingdomId,EnemyKingdomId=enemyKingdomId,AssignedDay=day};_wars[key]=x;}
            x.OfficialId=officialId;x.Focus=focus;x.AssignedDay=day;x.LastDecisionDay=-1;x.LastAction="受命总理此战，正在整顿守备、野战、攻城与拦截部署";Normalize(x);return x;
        }
        public bool Revoke(string kingdomId,string enemyKingdomId,out WarAdministration removed){string key=Key(kingdomId,enemyKingdomId);if(_wars.TryGetValue(key,out removed)){_wars.Remove(key);return true;}removed=null;return false;}
        public bool SetFocus(string kingdomId,string enemyKingdomId,WarAdministrationFocus focus){WarAdministration x=Get(kingdomId,enemyKingdomId);if(x==null)return false;x.Focus=focus;return true;}
        public Person BestOfficial(string kingdomId,WarAdministrationFocus focus){Person best=null;float bestScore=float.MinValue;foreach(Person p in _people.Values){if(!IsOfficialValid(kingdomId,p.Id)||OfficialBusyElsewhere(kingdomId,p.Id))continue;WarAdministrationProfile profile=Profile(p);float score=profile.Coordination*20f+profile.Logistics*10f;switch(focus){case WarAdministrationFocus.Defense:score+=profile.Defense*18f;break;case WarAdministrationFocus.FieldBattle:score+=profile.FieldBattle*18f;break;case WarAdministrationFocus.Siege:score+=profile.Siege*18f;break;case WarAdministrationFocus.Interception:score+=profile.Interception*18f;break;default:score+=(profile.Defense+profile.FieldBattle+profile.Siege+profile.Interception)*4.5f;break;}if(score>bestScore){bestScore=score;best=p;}}return best;}
        public WarAdministrationProfile Profile(WarAdministration x){Person p;if(x==null||!_people.TryGetValue(x.OfficialId,out p))return new WarAdministrationProfile();return Profile(p);}
        public WarAdministrationProfile Profile(Person p){
            var r=new WarAdministrationProfile();if(p==null)return r;OfficialRuntimeProfile o=TraitEffectEngine.OfficialProfile(p,_data);
            float admin=Mathx.Clamp(p.Stats.Administration/100f,0f,1.25f),org=Mathx.Clamp(p.Stats.Organization/100f,0f,1.25f),intel=Mathx.Clamp(p.Stats.Intelligence/100f,0f,1.25f),log=Mathx.Clamp(p.Stats.Logistics/100f,0f,1.25f),eng=Mathx.Clamp(p.Stats.Engineering/100f,0f,1.25f),mil=Mathx.Clamp(p.Stats.Military/100f,0f,1.25f);
            r.Coordination=Mathx.Clamp(.55f+(admin+org+intel)/3f*.72f,.55f,1.45f)*Mathx.Clamp((o.Military+o.Oversight)*.5f,.8f,1.35f);
            r.Defense=Mathx.Clamp(.58f+(mil+org+eng)/3f*.68f,.58f,1.42f)*Mathx.Clamp((o.Fortification+o.Military)*.5f,.8f,1.4f);
            r.FieldBattle=Mathx.Clamp(.55f+(mil+org+intel)/3f*.72f,.55f,1.45f)*Mathx.Clamp(o.Military,.8f,1.4f);
            r.Siege=Mathx.Clamp(.52f+(eng+mil+log)/3f*.75f,.52f,1.45f)*Mathx.Clamp((o.Construction+o.Fortification+o.Logistics)/3f,.8f,1.4f);
            r.Interception=Mathx.Clamp(.55f+(intel+org+mil)/3f*.72f,.55f,1.45f)*Mathx.Clamp((o.Oversight+o.Military)*.5f,.8f,1.4f);
            r.Logistics=Mathx.Clamp(.55f+(log+org+admin)/3f*.72f,.55f,1.45f)*Mathx.Clamp(o.Logistics,.8f,1.45f);
            r.ReactionRadius=Mathx.Clamp(6+(int)Math.Round((intel+org)*3.5f),6,15);r.MaxOrders=Mathx.Clamp(1+(int)Math.Floor((p.Stats.Administration+p.Stats.Organization+p.Stats.Military)/95f),1,4);return r;
        }
        static void Normalize(WarAdministration x){if(x==null)return;if(string.IsNullOrEmpty(x.LastAction))x.LastAction="尚未下达战务";}
    }

    public sealed class WarAdministrationProfile {
        public float Coordination=1f,Defense=1f,FieldBattle=1f,Siege=1f,Interception=1f,Logistics=1f;
        public int ReactionRadius=8,MaxOrders=2;
    }
}
