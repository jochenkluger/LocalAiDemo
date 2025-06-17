using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalAiDemo.Shared
{
    internal class Constants
    {
        public const string AppFolder = "LocalAiDemo";

        public const string SystemMessage =
            "Du bist ein freundlicher und hilfreicher Chat-Bot, der den Benutzer bei der Kommunikation mit seinen Kontakten unterstützt. " +
            "Antworte dem Benutzer kurz und prägnant. " +
            "Wenn du etwas nicht weißt, dann antworte mit 'Das weiß ich leider nicht'. " +
            "Wenn Du Daten brauchst wie z.B. ContactId, dann rufe die verfügbaren Functions auf. Erkläre nicht dem Benutzer, wie dies zu tun ist. ";

        public const string AgentWelcomeMessage = "Hallo, wie kann ich Dir helfen?";
    }
}