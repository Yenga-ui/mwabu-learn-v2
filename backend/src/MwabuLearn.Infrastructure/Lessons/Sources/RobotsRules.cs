using System.Text.RegularExpressions;
namespace MwabuLearn.Infrastructure.Lessons.Sources;

public static class RobotsRules
{
    public static bool Allows(string robots, string path)
    {
        if (robots.Length > 128000 || robots.Count(x => x == '\n') > 2000) return false;
        path = Uri.UnescapeDataString(path);
        var groups = new List<(List<string> Agents, List<(bool Allow, string Path)> Rules)>();
        List<string> agents = []; List<(bool, string)> rules = []; var directives = false;
        foreach (var raw in robots.Split('\n'))
        {
            var line = raw.Split('#')[0].Trim(); var colon = line.IndexOf(':'); if (colon < 0) continue;
            var key = line[..colon].Trim().ToLowerInvariant(); var value = line[(colon + 1)..].Trim();
            if (key == "user-agent")
            { if (directives) { groups.Add((agents, rules)); agents = []; rules = []; directives = false; } agents.Add(value.ToLowerInvariant()); }
            else if (key is "allow" or "disallow" && agents.Count > 0)
            { directives = true; if (value.Length > 0 && value.Length <= 2000) rules.Add((key == "allow", value)); }
        }
        if (agents.Count > 0) groups.Add((agents, rules));
        var specific = groups.Where(g => g.Agents.Any(a => a != "*" && "mwabulessonplanner".Contains(a, StringComparison.Ordinal))).ToList();
        var selected = specific.Count > 0 ? specific : groups.Where(g => g.Agents.Contains("*")).ToList();
        var matches = selected.SelectMany(x => x.Rules).Where(r => Matches(r.Path, path)).OrderByDescending(r => r.Path.Replace("*", "").TrimEnd('$').Length).ThenByDescending(r => r.Allow).ToList();
        return matches.Count == 0 || matches[0].Allow;
    }
    private static bool Matches(string pattern, string path)
    {
        var end = pattern.EndsWith('$'); var expression = "^" + Regex.Escape(pattern.TrimEnd('$')).Replace("\\*", ".*") + (end ? "$" : "");
        return Regex.IsMatch(path, expression, RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(100));
    }
}
