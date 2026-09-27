using System; using System.Collections.Generic;
namespace LordWar.Society {
 public sealed class FamilyOwner {
  readonly Dictionary<string,Family> _families; readonly Dictionary<string,Person> _people;
  public FamilyOwner(Dictionary<string,Family> families,Dictionary<string,Person> people){_families=families;_people=people;}
  public Family Create(string name,string familyType,string homeCity,string color,string tradition){var f=new Family{Id=Ids.Next("FAM"),Name=name,FamilyType=familyType,HomeCityId=homeCity,ColorHex=color,MilitaryTradition=tradition,Wealth=300,Prestige=20,Loyalty=65,Ambition=30,Influence=15};_families[f.Id]=f;return f;}
  public void AddMember(Family f,Person p){if(f==null||p==null)return;p.FamilyId=f.Id;if(!f.MemberIds.Contains(p.Id))f.MemberIds.Add(p.Id);}
  public float RebellionRisk(Family f,int lordPrestige,int unpaidMonths,int grievance){if(f==null)return 0;float r=f.Ambition*.28f+f.Influence*.22f+(100-f.Loyalty)*.32f+unpaidMonths*6f+grievance*.25f-lordPrestige*.18f;return Mathx.Clamp(r,0,100);}
  public bool CanSupplyOfficial(Person p){return p!=null&&p.Alive&&p.Noble&&!p.Merchant;}
 }
}
