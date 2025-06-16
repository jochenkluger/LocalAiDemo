using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalAiDemo.Shared.Services.Generation
{
    public interface IAiAssistantService
    {
        Task<string> GetResponseAsync(string prompt);

        string GetAssistantName();
    }
}