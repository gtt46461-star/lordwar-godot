using System; using System.Collections.Generic; using LordWar.Data; using LordWar.Economy;
namespace LordWar.Governance {
 public sealed class PolicyOwner {
  readonly GameDataCatalog _data; readonly ProposalSystem _proposals; readonly EconomyLedger _economy;
  public PolicyOwner(GameDataCatalog data,ProposalSystem proposals,EconomyLedger economy){_data=data;_proposals=proposals;_economy=economy;}
  bool HasTrial(Kingdom k,PolicyDef d){if(d==null||k==null)return false;if(d.Stage!="成制")return true;foreach(string id in k.ActivePolicyIds){PolicyDef x;if(_data.Policies.TryGetValue(id,out x)&&x.Name==d.Name&&x.Stage=="试行")return true;}return false;}
  bool AlreadyRepresented(Kingdom k,string policyId){if(k.ActivePolicyIds.Contains(policyId)||k.SuspendedPolicyIds.Contains(policyId))return true;foreach(Proposal p in _proposals.Queue)if(p.DataId==policyId&&(p.State==ProposalState.Pending||p.State==ProposalState.Approved||p.State==ProposalState.Running))return true;return false;}
  int ProposalAbility(Person official,PolicyDef d){
   if(official==null||d==null)return 0;string c=d.Category??"";int ability;
   if(c.IndexOf("农业")>=0||c.IndexOf("人口")>=0||c.IndexOf("财政")>=0||c.IndexOf("官员")>=0||c.IndexOf("贵族")>=0||c.IndexOf("治安")>=0) ability=Math.Max(official.Stats.Administration,(official.Stats.Administration+official.Stats.Intelligence)/2);
   else if(c.IndexOf("市场")>=0||c.IndexOf("外交")>=0) ability=Math.Max(official.Stats.Intelligence,(official.Stats.Administration+official.Stats.Intelligence)/2);
   else if(c.IndexOf("军粮")>=0||c.IndexOf("后勤")>=0||c.IndexOf("骑兵")>=0) ability=Math.Max(official.Stats.Logistics,(official.Stats.Logistics+official.Stats.Organization)/2);
   else if(c.IndexOf("城防")>=0||c.IndexOf("道路")>=0||c.IndexOf("军械")>=0) ability=Math.Max(official.Stats.Engineering,(official.Stats.Engineering+official.Stats.Organization)/2);
   else if(c.IndexOf("基础军制")>=0||c.IndexOf("军功")>=0) ability=Math.Max(official.Stats.Military,(official.Stats.Organization+official.Stats.Military)/2);
   else ability=official.Stats.Administration;
   ability+=TraitEffectEngine.OfficialProposalBonus(official,_data,d.Category);return Mathx.Clamp(ability,0,140);
  }
  public bool Propose(string policyId,Person official,Kingdom kingdom,int day){PolicyDef d;if(!_data.Policies.TryGetValue(policyId,out d)||official==null||kingdom==null||official.Class!=SocialClass.Official)return false;if(AlreadyRepresented(kingdom,policyId)||!HasTrial(kingdom,d))return false;int ability=ProposalAbility(official,d);if(ability<d.Ability)return false;return _proposals.SubmitPolicy(d.Id,d.DisplayName,official.Id,ability,d.Ability,d.Cost,d.Days,day);}
  public bool StartApproved(Proposal p,Kingdom k,int day){if(p==null||k==null||p.State!=ProposalState.Approved)return false;PolicyDef d;if(!_data.Policies.TryGetValue(p.DataId,out d)||!HasTrial(k,d))return false;if(!_economy.Pay(k,d.Cost))return false;_proposals.MarkRunning(p,day);return true;}
  public void ApplyCompleted(Proposal p,Kingdom k,City c){if(p==null||p.State!=ProposalState.Completed||k==null)return;PolicyDef d;if(!_data.Policies.TryGetValue(p.DataId,out d))return;if(k.ActivePolicyIds.Contains(d.Id))return;k.SuspendedPolicyIds.Remove(d.Id);k.ActivePolicyIds.Add(d.Id);string cat=d.Category??"";if(c!=null){if(cat.IndexOf("农业")>=0){c.FarmJobs+=4;c.Food+=30;}else if(cat.IndexOf("商业")>=0||cat.IndexOf("市场")>=0)c.MarketJobs+=3;else if(cat.IndexOf("矿")>=0||cat.IndexOf("军械")>=0)c.WorkshopJobs+=2;else if(cat.IndexOf("城防")>=0)c.PopulationCapacity+=5;}}
  public PolicyRuntimeProfile RuntimeProfile(Kingdom k){var r=new PolicyRuntimeProfile();if(k==null)return r;foreach(string id in k.ActivePolicyIds){PolicyDef d;if(_data.Policies.TryGetValue(id,out d))r.Apply(d);}return r;}
  public int ChargeMonthlyMaintenance(Kingdom k){if(k==null)return 0;int paid=0;var active=new List<string>(k.ActivePolicyIds);foreach(string id in active){PolicyDef d;if(!_data.Policies.TryGetValue(id,out d))continue;int due=Math.Max(0,(int)Math.Ceiling(d.Monthly));if(due==0)continue;if(_economy.Pay(k,due))paid+=due;else{k.ActivePolicyIds.Remove(id);if(!k.SuspendedPolicyIds.Contains(id))k.SuspendedPolicyIds.Add(id);}}return paid;}
  public int ReactivateAffordable(Kingdom k){if(k==null)return 0;int n=0;var suspended=new List<string>(k.SuspendedPolicyIds);foreach(string id in suspended){PolicyDef d;if(!_data.Policies.TryGetValue(id,out d))continue;int reserve=Math.Max(20,(int)Math.Ceiling(d.Monthly*3));if(k.Treasury<reserve)continue;k.SuspendedPolicyIds.Remove(id);if(!k.ActivePolicyIds.Contains(id))k.ActivePolicyIds.Add(id);n++;}return n;}
 }
}
