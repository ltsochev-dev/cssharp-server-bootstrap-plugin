using ServerBootstrap.Config.Dto;
using System.Text.Json;

namespace ServerBootstrap.Config {
    public static class AnnotationsParser
    {
        private static readonly HashSet<string> AllowedModes = new(StringComparer.OrdinalIgnoreCase)
                    {
                        "deathmatch",
                        "competitive",
                        "scrim",
                        "retake"
                    };

        public static AnnotationParseResult Parse(IReadOnlyDictionary<string, string>? annotations)
        {
            var errors = new List<string>();

            if (annotations == null)
            {
                errors.Add("Annotations collection is null.");
                return new AnnotationParseResult
                {
                    Errors = errors
                };
            }

            var mode = ReadRequiredString(annotations, "mode", errors);

            string? map = ReadOptionalString(annotations, "map");

            int? maxPlayers = ReadOptionalPositiveInt(annotations, "maxPlayers", errors);
            int? warmupTimeoutSeconds = ReadOptionalNonNegativeInt(annotations, "warmupTimeoutSeconds", errors);
            bool enableHltv = ReadOptionalBool(annotations, "enableHltv", errors) ?? false;

            TeamsDto? teams = null;
            if (annotations.TryGetValue("teams", out var teamsRaw) && !string.IsNullOrWhiteSpace(teamsRaw))
            {
                teams = ParseTeams(teamsRaw, errors);
            }

            if (!string.IsNullOrWhiteSpace(mode) && !AllowedModes.Contains(mode))
            {
                errors.Add($"Invalid mode '{mode}'. Allowed values: {string.Join(", ", AllowedModes)}.");
            }

            if (mode != null)
            {
                mode = mode.Trim().ToLowerInvariant();
            }

            if (mode is "competitive" or "scrim")
            {
                if (warmupTimeoutSeconds == null)
                {
                    errors.Add($"'{mode}' mode requires 'warmupTimeoutSeconds'.");
                }

                if (teams == null)
                {
                    errors.Add($"'{mode}' mode requires 'teams'.");
                }
                else
                {
                    if (teams.TeamA.Count == 0)
                        errors.Add("teams.teamA must contain at least one player.");

                    if (teams.TeamB.Count == 0)
                        errors.Add("teams.teamB must contain at least one player.");
                }
            }

            if (mode == "competitive" && enableHltv)
            {
                errors.Add("Competitive mode should not enable HLTV.");
            }

            if (errors.Count > 0)
            {
                return new AnnotationParseResult
                {
                    Errors = errors
                };
            }

            return new AnnotationParseResult
            {
                Data = new ServerAnnotationsDto
                {
                    Mode = mode!,
                    Map = map,
                    MaxPlayers = maxPlayers,
                    WarmupTimeoutSeconds = warmupTimeoutSeconds,
                    EnableHltv = enableHltv,
                    Teams = teams
                }
            };
        }

        private static string? ReadRequiredString(
            IReadOnlyDictionary<string, string> annotations,
            string key,
            List<string> errors)
        {
            if (!annotations.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"Missing required annotation '{key}'.");
                return null;
            }

            return value.Trim();
        }

        private static string? ReadOptionalString(
            IReadOnlyDictionary<string, string> annotations,
            string key)
        {
            if (!annotations.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

        private static int? ReadOptionalPositiveInt(
            IReadOnlyDictionary<string, string> annotations,
            string key,
            List<string> errors)
        {
            if (!annotations.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            if (!int.TryParse(raw.Trim(), out var value) || value <= 0)
            {
                errors.Add($"Annotation '{key}' must be a positive integer.");
                return null;
            }

            return value;
        }

        private static int? ReadOptionalNonNegativeInt(
            IReadOnlyDictionary<string, string> annotations,
            string key,
            List<string> errors)
        {
            if (!annotations.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            if (!int.TryParse(raw.Trim(), out var value) || value < 0)
            {
                errors.Add($"Annotation '{key}' must be a non-negative integer.");
                return null;
            }

            return value;
        }

        private static bool? ReadOptionalBool(
            IReadOnlyDictionary<string, string> annotations,
            string key,
            List<string> errors)
        {
            if (!annotations.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            if (!bool.TryParse(raw.Trim(), out var value))
            {
                errors.Add($"Annotation '{key}' must be 'true' or 'false'.");
                return null;
            }

            return value;
        }

        private static TeamsDto? ParseTeams(string rawJson, List<string> errors)
        {
            try
            {
                var teams = JsonSerializer.Deserialize<TeamsDto>(rawJson);

                if (teams == null)
                {
                    errors.Add("Annotation 'teams' could not be parsed.");
                    return null;
                }

                teams = new TeamsDto
                {
                    TeamA = NormalizePlayers(teams.TeamA),
                    TeamB = NormalizePlayers(teams.TeamB)
                };

                var duplicatePlayers = teams.TeamA
                    .Concat(teams.TeamB)
                    .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                if (duplicatePlayers.Count > 0)
                {
                    errors.Add($"Duplicate players found across teams: {string.Join(", ", duplicatePlayers)}.");
                }

                return teams;
            }
            catch (JsonException ex)
            {
                errors.Add($"Annotation 'teams' contains invalid JSON: {ex.Message}");
                return null;
            }
        }

        private static List<string> NormalizePlayers(IEnumerable<string>? players)
        {
            if (players == null)
                return new List<string>();

            return players
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}