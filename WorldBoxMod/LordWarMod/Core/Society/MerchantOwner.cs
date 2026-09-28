using System; using System.Collections.Generic;
namespace LordWar.Society {
 public sealed class MerchantOwner {
  readonly Dictionary<string,Person> _people; readonly List<MerchantInvestment> _investments=new List<MerchantInvestment>();
  public MerchantOwner(Dictionary<string,Person> people){_people=people;}
  public IEnumerable<MerchantInvestment> Investments{get{return _investments;}}
  public void Restore(IEnumerable<MerchantInvestment> source){_investments.Clear();if(source==null)return;foreach(var x in source)if(x!=null&&!string.IsNullOrEmpty(x.Id))_investments.Add(x);}
  public void DiscoverMerchants(City city,int wealthThreshold){foreach(string id in city.PersonIds){Person p;if(!_people.TryGetValue(id,out p)||!p.Alive||p.Noble||p.Job==JobKind.Soldier)continue;if(p.Wealth>=wealthThreshold){p.Merchant=true;p.Class=SocialClass.Merchant;p.Job=JobKind.Trader;}}}
  public int ActiveInvestmentCountForGeneral(string generalId){int n=0;foreach(var i in _investments)if(i.Active&&i.GeneralId==generalId)n++;return n;}
  public int ActiveInvestmentCount(){int n=0;foreach(var i in _investments)if(i.Active)n++;return n;}
  public Proposal CreateInvestmentProposal(Person merchant,Army army,int principal,int monthly,int day,float taxReduction=.55f){
   if(merchant==null||army==null||!merchant.Merchant||merchant.Noble||merchant.Wealth<principal||HasActiveInvestment(merchant.Id))return null;
   var p=new Proposal{Id=Ids.Next("INVREQ"),Title="商人投资军资申请",Description=merchant.Name+"拟向"+army.Name+"提供军资，批准后其商税将显著下降。",Kind=ProposalKind.MerchantInvestment,ProposerId=merchant.Id,TargetId=army.Id,CostGold=0,RequiredAbility=0,DurationDays=0,SubmittedDay=day};
   p.SetEffect("principal",principal);p.SetEffect("monthly",monthly);p.SetEffect("tax_reduction",Mathx.Clamp(taxReduction,.2f,.75f));return p;
  }
  public MerchantInvestment Activate(Person merchant,Army army,Proposal proposal,int day,int maxInvestorsForGeneral=4){if(merchant==null||army==null||proposal==null||proposal.State!=ProposalState.Approved||HasActiveInvestment(merchant.Id)||ActiveInvestmentCountForGeneral(army.GeneralId)>=Math.Max(1,maxInvestorsForGeneral))return null;int principal=(int)proposal.GetEffect("principal"),monthly=(int)proposal.GetEffect("monthly");if(merchant.Wealth<principal)return null;merchant.Wealth-=principal;army.Finance.Gold+=principal;var c=new MerchantInvestment{Id=Ids.Next("INV"),MerchantId=merchant.Id,GeneralId=army.GeneralId,Principal=principal,MonthlySupport=monthly,TaxReductionPct=proposal.GetEffect("tax_reduction"),StartDay=day,Active=true};_investments.Add(c);return c;}
  public void MonthlySupport(Dictionary<string,Army> armies){foreach(var i in _investments){if(!i.Active)continue;Person merchant;if(!_people.TryGetValue(i.MerchantId,out merchant)||!merchant.Alive||merchant.Wealth<i.MonthlySupport){i.Active=false;continue;}Army army=null;foreach(var a in armies.Values)if(a.GeneralId==i.GeneralId){army=a;break;}if(army==null){i.Active=false;continue;}merchant.Wealth-=i.MonthlySupport;army.Finance.Gold+=i.MonthlySupport;}}
  public int EmergencyLoan(string generalId,MilitaryAccount account,int requested){if(account==null||requested<=0)return 0;int remaining=requested,total=0;foreach(var i in _investments){if(!i.Active||i.GeneralId!=generalId)continue;Person m;if(!_people.TryGetValue(i.MerchantId,out m)||!m.Alive||m.Wealth<=0)continue;int lend=Math.Min(remaining,Math.Min(m.Wealth/3,200));if(lend<=0)continue;m.Wealth-=lend;Military.MilitaryFinanceSystem.BorrowFromMerchant(account,lend);total+=lend;remaining-=lend;if(remaining<=0)break;}return total;}
  public bool HasActiveInvestment(string merchantId){foreach(var i in _investments)if(i.Active&&i.MerchantId==merchantId)return true;return false;}
  public float TaxReductionFor(string merchantId){float r=0;foreach(var i in _investments)if(i.Active&&i.MerchantId==merchantId)r=Math.Max(r,i.TaxReductionPct);return r;}
 }
}
