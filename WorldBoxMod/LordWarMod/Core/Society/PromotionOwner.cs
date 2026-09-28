using System.Collections.Generic;
using LordWar.Governance;
namespace LordWar.Society {
 public sealed class PromotionOwner {
  readonly Dictionary<string,Person> _people; readonly ProposalSystem _proposals;
  public PromotionOwner(Dictionary<string,Person> people,ProposalSystem proposals){_people=people;_proposals=proposals;}
  bool HasPending(string personId,string dataId){foreach(Proposal p in _proposals.Queue)if(p.State==ProposalState.Pending&&p.TargetId==personId&&p.DataId==dataId)return true;return false;}
  public Proposal ConsiderOfficer(Person p,int day){if(p==null||!p.Alive||p.Job!=JobKind.Soldier||!p.HasCommandPotential||p.Experience<100||p.Battles<2||HasPending(p.Id,"officer"))return null;var q=new Proposal{Id=Ids.Next("PROM"),Title="士兵晋升军官申请",Description=p.Name+"因长期训练与战功进入军官候选。",Kind=ProposalKind.Promotion,DataId="officer",ProposerId=p.Id,TargetId=p.Id,SubmittedDay=day};return _proposals.Submit(q,100)?q:null;}
  public Proposal ConsiderNoble(Person p,int day){if(p==null||!p.Alive||p.Noble||p.Class!=SocialClass.Officer||p.Experience<180||p.Battles<4||HasPending(p.Id,"noble"))return null;var q=new Proposal{Id=Ids.Next("PROM"),Title="军功授爵申请",Description=p.Name+"凭长期军功申请晋升贵族。",Kind=ProposalKind.Promotion,DataId="noble",ProposerId=p.Id,TargetId=p.Id,SubmittedDay=day};return _proposals.Submit(q,100)?q:null;}
  public Proposal ConsiderIndependentGeneral(Person p,int day){if(p==null||!p.Alive||!p.Noble||p.Class!=SocialClass.Officer||p.Experience<260||p.Battles<6||HasPending(p.Id,"general_independent"))return null;var q=new Proposal{Id=Ids.Next("PROM"),Title="独立将军任命申请",Description=p.Name+"请求脱离原部独立领军；若拒绝则继续留任高级副将。",Kind=ProposalKind.Promotion,DataId="general_independent",ProposerId=p.Id,TargetId=p.Id,SubmittedDay=day};return _proposals.Submit(q,100)?q:null;}
  public bool ApproveOfficer(Person p){if(p==null||!p.Alive||!p.HasCommandPotential)return false;p.Class=SocialClass.Officer;p.Job=JobKind.Officer;p.Promotions++;p.Stats.Command+=8;p.Stats.Prestige+=5;return true;}
 }
}
