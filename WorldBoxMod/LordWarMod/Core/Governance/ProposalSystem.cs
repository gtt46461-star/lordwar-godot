using System;
using System.Collections.Generic;
namespace LordWar.Governance {
    public sealed class ProposalSystem {
        readonly List<Proposal> _queue=new List<Proposal>(); readonly Dictionary<string,Proposal> _byId=new Dictionary<string,Proposal>();
        public IList<Proposal> Queue { get { return _queue.AsReadOnly(); } }
        public int PendingCount { get { int n=0;foreach(var p in _queue)if(p.State==ProposalState.Pending)n++;return n; } }
        public bool Submit(Proposal p,int proposerAbility){if(p==null||p.State!=ProposalState.Pending||proposerAbility<p.RequiredAbility)return false;if(string.IsNullOrEmpty(p.Id))p.Id=Ids.Next("REQ");if(_byId.ContainsKey(p.Id))return false;_queue.Add(p);_byId[p.Id]=p;return true;}
        public bool SubmitPolicy(string policyId,string displayName,string proposerId,int proposerAbility,int required,int cost,int days,int currentDay){return Submit(new Proposal{Id=Ids.Next("POL"),Title=displayName,Kind=ProposalKind.Policy,DataId=policyId,ProposerId=proposerId,RequiredAbility=required,CostGold=cost,DurationDays=days,SubmittedDay=currentDay},proposerAbility);}
        public bool Approve(string id){Proposal p;if(!_byId.TryGetValue(id,out p)||p.State!=ProposalState.Pending)return false;p.State=ProposalState.Approved;return true;}
        public bool Reject(string id){Proposal p;if(!_byId.TryGetValue(id,out p)||p.State!=ProposalState.Pending)return false;p.State=ProposalState.Rejected;return true;}
        public Proposal NextApproved(){foreach(var p in _queue)if(p.State==ProposalState.Approved)return p;return null;}
        public void MarkRunning(Proposal p,int day){if(p!=null&&p.State==ProposalState.Approved){p.State=ProposalState.Running;p.StartedDay=day;}}
        public void Tick(int day){foreach(var p in _queue)if(p.State==ProposalState.Running&&day-p.StartedDay>=p.DurationDays)p.State=ProposalState.Completed;}
        public int PruneResolved(int maxResolved){int resolved=0;for(int i=0;i<_queue.Count;i++)if(_queue[i].State!=ProposalState.Pending&&_queue[i].State!=ProposalState.Approved&&_queue[i].State!=ProposalState.Running)resolved++;int remove=Math.Max(0,resolved-Math.Max(0,maxResolved));if(remove<=0)return 0;int removed=0;for(int i=0;i<_queue.Count&&removed<remove;){Proposal p=_queue[i];if(p.State!=ProposalState.Pending&&p.State!=ProposalState.Approved&&p.State!=ProposalState.Running){_queue.RemoveAt(i);_byId.Remove(p.Id);removed++;}else i++;}return removed;}
        public void Restore(IEnumerable<Proposal> source){_queue.Clear();_byId.Clear();if(source==null)return;foreach(var p in source){if(p==null||string.IsNullOrEmpty(p.Id))continue;_queue.Add(p);_byId[p.Id]=p;}}
    }
}
