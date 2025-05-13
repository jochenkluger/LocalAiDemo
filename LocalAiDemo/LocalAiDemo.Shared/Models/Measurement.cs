using System;

namespace LocalAiDemo.Shared.Models
{
    public class Measurement
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double Value { get; set; }
        public string Unit { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
