namespace LordWar {
    public enum WorldCommandKind { Pause, Resume, SetSpeed, AdvanceDay }
    /// <summary>UI command identity permits idempotent handling of repeated touch events.</summary>
    public sealed class WorldCommand {
        public string Id;
        public WorldCommandKind Kind;
        public float Value;
        public WorldCommand(string id,WorldCommandKind kind,float value=0f){Id=id;Kind=kind;Value=value;}
    }
}
