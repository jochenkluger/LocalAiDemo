using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalAiDemo.Shared.Models
{
    public class AppConfiguration
    {
        public string TtsProvider { get; set; } = "Browser";
        public string SttProvider { get; set; } = "Browser";
        public string GenerationProvider { get; set; } = "phi4";
    }
}