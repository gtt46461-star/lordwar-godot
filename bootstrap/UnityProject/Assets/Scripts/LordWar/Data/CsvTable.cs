using System;
using System.Collections.Generic;
using System.Text;
namespace LordWar.Data {
    public sealed class CsvTable {
        public readonly List<string> Headers = new List<string>();
        public readonly List<Dictionary<string,string>> Rows = new List<Dictionary<string,string>>();
        public static CsvTable Parse(string text) {
            CsvTable t=new CsvTable(); if(string.IsNullOrEmpty(text)) return t;
            List<List<string>> lines=Read(text); if(lines.Count==0) return t;
            foreach(string h0 in lines[0]) { string h=h0; if(h.Length>0 && (int)h[0]==0xFEFF) h=h.Substring(1); t.Headers.Add(h.Trim()); }
            for(int i=1;i<lines.Count;i++) { if(lines[i].Count==1 && string.IsNullOrWhiteSpace(lines[i][0])) continue; var row=new Dictionary<string,string>(); for(int c=0;c<t.Headers.Count;c++) row[t.Headers[c]]=c<lines[i].Count?lines[i][c]:""; t.Rows.Add(row); }
            return t;
        }
        static List<List<string>> Read(string s) {
            var all=new List<List<string>>(); var row=new List<string>(); var cell=new StringBuilder(); bool q=false;
            for(int i=0;i<s.Length;i++) { char ch=s[i]; if(q) { if(ch=='"' && i+1<s.Length && s[i+1]=='"'){cell.Append('"');i++;} else if(ch=='"') q=false; else cell.Append(ch); }
                else { if(ch=='"') q=true; else if(ch==','){row.Add(cell.ToString());cell.Length=0;} else if(ch=='\n'){row.Add(cell.ToString().TrimEnd('\r'));cell.Length=0;all.Add(row);row=new List<string>();} else cell.Append(ch); } }
            row.Add(cell.ToString().TrimEnd('\r')); all.Add(row); return all;
        }
    }
}
