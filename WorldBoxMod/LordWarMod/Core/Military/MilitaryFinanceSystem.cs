using System;
namespace LordWar.Military {
    public static class MilitaryFinanceSystem {
        public static int MonthlyCost(MilitaryAccount a,float costMultiplier=1f){if(a==null)return 0;int raw=Math.Max(0,a.MonthlyWages+a.MonthlyFoodCost+a.ExpectedRepairCost+a.HorseCost+a.MedicalCost+a.SupplyCost);return Math.Max(0,(int)Math.Ceiling(raw*Mathx.Clamp(costMultiplier,.65f,1.75f)));}
        public static int PayMonth(MilitaryAccount a,float costMultiplier=1f){if(a==null)return 0;int need=MonthlyCost(a,costMultiplier);int paid=Math.Min(a.Gold,need);a.Gold-=paid;int shortfall=need-paid;if(shortfall>0)a.MonthsInArrears++;else a.MonthsInArrears=Math.Max(0,a.MonthsInArrears-1);return shortfall;}
        public static void BorrowFromLord(MilitaryAccount a,int gold){if(a==null||gold<=0)return;a.Gold+=gold;a.DebtToLord+=gold;}
        public static void BorrowFromMerchant(MilitaryAccount a,int gold){if(a==null||gold<=0)return;a.Gold+=gold;a.DebtToMerchants+=gold;}
        public static int RepayDebts(MilitaryAccount a,int available){if(a==null||available<=0)return 0;int paid=0,toMerchant=Math.Min(a.DebtToMerchants,available);a.DebtToMerchants-=toMerchant;available-=toMerchant;paid+=toMerchant;int toLord=Math.Min(a.DebtToLord,available);a.DebtToLord-=toLord;paid+=toLord;return paid;}
        public static float LoyaltyPenalty(MilitaryAccount a,float debtGrace=1f){if(a==null)return 0;float grace=Mathx.Clamp(debtGrace,.65f,1.8f);float debt=(a.DebtToLord+a.DebtToMerchants)/(500f*grace);return Math.Min(35f,(a.MonthsInArrears*5f)/grace+debt);}
        public static float PoliticalRisk(MilitaryAccount a,float debtGrace=1f){if(a==null)return 0f;float debt=(a.DebtToLord+a.DebtToMerchants)/800f;float arrears=a.MonthsInArrears*.12f;return Mathx.Clamp((debt+arrears)/Mathx.Clamp(debtGrace,.7f,1.8f),0f,1f);}
    }
}
