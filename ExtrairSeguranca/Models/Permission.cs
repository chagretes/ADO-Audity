namespace ExtrairSeguranca.Models
{
    public class Permission
    {
        public string Repository { get; set; }
        public string Name { get; set; }
        public string Group { get; set; }
        public string ID { get; set; }
        public string Allow { get; set; }
        public string Deny { get; set; }
        public string Members { get; set; }
    }
}
