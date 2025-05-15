namespace LocalAiDemo.Shared.Models
{
    public class Person
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public string Status { get; set; } = "Offline"; // Online, Offline, Away, etc.
        public DateTime LastSeen { get; set; } = DateTime.Now;
        public string Department { get; set; } = string.Empty; // For organizations: Sales, Support, Engineering, etc.
    }
}