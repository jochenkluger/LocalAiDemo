using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalAiDemo.Shared.Models
{
    public enum ExecutionMode
    {
        Local, // Lokale Ausführung
        Server // Server-Ausführung
    }

    public class AiModel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string FileName { get; set; }
        public string Url { get; set; }
        public bool IsSelected { get; set; }
        public ExecutionMode ExecutionMode { get; set; }

        public AiModel()
        {
        }

        public AiModel(string name, string fileName, string url,
            ExecutionMode executionMode = ExecutionMode.Local, bool isSelected = false)
        {
            Name = name;
            FileName = fileName;
            Url = url;
            ExecutionMode = executionMode;
            IsSelected = isSelected;
        }
    }
}