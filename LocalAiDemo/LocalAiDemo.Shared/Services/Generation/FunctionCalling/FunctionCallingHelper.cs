using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LocalAiDemo.Shared.Services.FunctionCalling
{
    /// <summary>
    /// Klasse zum Extrahieren und Ausführen von Funktionen aus LLM-Antworten
    /// </summary>
    public static class FunctionCallingHelper
    {
        // JSON-Serialisierungsoptionen
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Beschreibt eine Funktion für das LLM
        /// </summary>
        public class FunctionDescription
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public List<FunctionParameter> Parameters { get; set; } = new List<FunctionParameter>();
        }

        /// <summary>
        /// Beschreibt einen Parameter einer Funktion
        /// </summary>
        public class FunctionParameter
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public string Description { get; set; }
            public bool Required { get; set; } = true;
        }

        /// <summary>
        /// Stellt einen Funktionsaufruf aus der LLM-Antwort dar
        /// </summary>
        public class FunctionCall
        {
            public string Name { get; set; }
            public Dictionary<string, object> Arguments { get; set; }
        }

        /// <summary>
        /// Erstellt eine Funktionsbeschreibung für die CreateMessage-Funktion
        /// </summary>
        public static FunctionDescription GetCreateMessageFunctionDescription()
        {
            return new FunctionDescription
            {
                Name = "CreateMessage",
                Description =
                    "Erstellt eine Nachricht mit dem angegebenen Text für einen Empfänger. WICHTIG: Erfinde keine contactId. Du musst zuerst die verfügbaren Kontakte abrufen (GetAvailableContacts, SearchContacts oder GetContactByName) um die Kontakt-ID zu erhalten, bevor du eine Nachricht erstellen kannst. Wenn Du eine Nachricht erstellst, Beginne immer mit einer freundlichen Begrüßung und ende mit 'Viele Grüße \r\nJochen Kluger'. Formatiere die Nachricht ordentlich mit Zeilenumbrüchen.",
                Parameters = new List<FunctionParameter>
                {
                    new FunctionParameter
                    {
                        Name = "contactId",
                        Type = "integer",
                        Description =
                            "ID des Empfänger-Kontakts (muss zuerst über GetAvailableContacts, SearchContacts oder GetContactByName ermittelt werden)",
                        Required = true
                    },
                    new FunctionParameter
                    {
                        Name = "messageText",
                        Type = "string",
                        Description = "Text der Nachricht",
                        Required = true
                    }
                }
            };
        }

        /// <summary>
        /// Erstellt eine Funktionsbeschreibung für die GetAvailableContacts-Funktion
        /// </summary>
        public static FunctionDescription GetAvailableContactsFunctionDescription()
        {
            return new FunctionDescription
            {
                Name = "GetAvailableContacts",
                Description =
                    "Ruft alle verfügbaren Kontakte ab, um zu sehen, wer für Nachrichten verfügbar ist. Die Antwort enthält die Kontakt-ID die für das Erstellen von Nachrichten benötigt wird.",
                Parameters = new List<FunctionParameter>()
            };
        }

        /// <summary>
        /// Erstellt eine Funktionsbeschreibung für die SearchContacts-Funktion
        /// </summary>
        public static FunctionDescription GetSearchContactsFunctionDescription()
        {
            return new FunctionDescription
            {
                Name = "SearchContacts",
                Description =
                    "Sucht nach Kontakten basierend auf einem Suchbegriff (Name, E-Mail oder Notizen). Die Antwort enthält die Kontakt-ID die für das Erstellen von Nachrichten benötigt wird.",
                Parameters = new List<FunctionParameter>
                {
                    new FunctionParameter
                    {
                        Name = "searchTerm",
                        Type = "string",
                        Description = "Suchbegriff für die Kontaktsuche",
                        Required = true
                    }
                }
            };
        }

        /// <summary>
        /// Erstellt eine Funktionsbeschreibung für die GetContactByName-Funktion
        /// </summary>
        public static FunctionDescription GetContactByNameFunctionDescription()
        {
            return new FunctionDescription
            {
                Name = "GetContactByName",
                Description =
                    "Ruft einen spezifischen Kontakt nach Namen ab, um Details zu erhalten. Die Antwort enthält die Kontakt-ID die für das Erstellen von Nachrichten benötigt wird.",
                Parameters = new List<FunctionParameter>
                {
                    new FunctionParameter
                    {
                        Name = "contactName",
                        Type = "string",
                        Description = "Name des gesuchten Kontakts",
                        Required = true
                    }
                }
            };
        }

        /// <summary>
        /// Ruft alle verfügbaren Funktionsbeschreibungen ab (inkl. Kontakt-Funktionen)
        /// </summary>
        public static List<FunctionDescription> GetAllFunctionDescriptions()
        {
            return new List<FunctionDescription>
            {
                GetCreateMessageFunctionDescription(),
                GetAvailableContactsFunctionDescription(),
                GetSearchContactsFunctionDescription(),
                GetContactByNameFunctionDescription()
            };
        }

        /// <summary>
        /// Generiert eine Systemnachricht mit Funktionsdefinitionen
        /// </summary>
        public static string GenerateFunctionSystemMessage(List<FunctionDescription> functions)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Du kannst die folgenden Funktionen aufrufen:");

            foreach (var func in functions)
            {
                sb.AppendLine($"## {func.Name}");
                sb.AppendLine(func.Description);
                sb.AppendLine("Parameter:");

                foreach (var param in func.Parameters)
                {
                    sb.AppendLine($"- {param.Name} ({param.Type}): {param.Description}" +
                                  (param.Required ? " (erforderlich)" : " (optional)"));
                }

                sb.AppendLine();
            }

            sb.AppendLine(
                "Um eine Funktion aufzurufen, formatiere deine Antwort wie folgt und antworte nur mit der JSON Struktur:");
            sb.AppendLine("```json");
            sb.AppendLine("{");
            sb.AppendLine("  \"function\": \"FunktionsName\",");
            sb.AppendLine("  \"arguments\": {");
            sb.AppendLine("    \"parameter1\": \"wert1\",");
            sb.AppendLine("    \"parameter2\": \"wert2\"");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            sb.AppendLine("```");

            return sb.ToString();
        }

        /// <summary>
        /// Extrahiert einen Funktionsaufruf aus der LLM-Antwort
        /// </summary>
        public static FunctionCall ExtractFunctionCall(string llmResponse)
        {
            try
            {
                // Regex, um JSON zwischen ```json und ``` oder ```JSON und ``` zu finden
                var jsonMatch = Regex.Match(llmResponse, @"```(?:json|JSON)\s*\n([\s\S]*?)\n\s*```");
                if (jsonMatch.Success && jsonMatch.Groups.Count > 1)
                {
                    var json = jsonMatch.Groups[1].Value.Trim();
                    var callData = JsonSerializer.Deserialize<JsonElement>(json, _jsonOptions);

                    if (callData.TryGetProperty("function", out var functionNameElement) &&
                        callData.TryGetProperty("arguments", out var argumentsElement))
                    {
                        var functionName = functionNameElement.GetString();
                        var arguments = new Dictionary<string, object>();

                        foreach (var arg in argumentsElement.EnumerateObject())
                        {
                            arguments[arg.Name] = arg.Value.ValueKind switch
                            {
                                JsonValueKind.String => arg.Value.GetString(),
                                JsonValueKind.Number => arg.Value.GetDouble(),
                                JsonValueKind.True => true,
                                JsonValueKind.False => false,
                                _ => null
                            };
                        }

                        return new FunctionCall
                        {
                            Name = functionName,
                            Arguments = arguments
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Extrahieren des Funktionsaufrufs: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Generiert eine formatierte Antwort nach einem Funktionsaufruf
        /// </summary>
        public static string GenerateFunctionResponse(string functionName, string result)
        {
            return $"Funktion {functionName} wurde erfolgreich ausgeführt. Ergebnis: {result}";
        }
    }
}