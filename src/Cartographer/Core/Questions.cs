using System.Text;
using System.Text.RegularExpressions;

namespace Cartographer;

public sealed class Question
{
    public string Id = "";
    public string State = "open";
    public string RaisedBy = "cli";
    public int Attempts;
    public string Missing = "";
    public string WaitingOn = "person";
    public string AnswerLooksLike = "";
    public string Answer = "";

    public static readonly string[] States = ["open", "needs-person", "answered", "dismissed"];
    public static readonly string[] Raisers = ["cli", "agent", "person"];
    public string Key => Regex.Replace(Missing.ToLowerInvariant(), @"\s+", " ").Trim();
}

/// <summary>The Open questions section of an entry body, in the fixed layout of spec section 3.</summary>
public sealed class QuestionList
{
    public List<Question> Items = [];
    public List<string> Dismissed = [];
    public List<string> Problems = [];

    public static QuestionList Parse(string body)
    {
        var list = new QuestionList();
        var section = Sections.Get(body, Sections.Questions);
        if (section.Trim().Length == 0) return list;
        foreach (var block in Regex.Split("\n" + section, @"\n(?=### )").Where(b => b.Trim().Length > 0))
        {
            var lines = block.Trim('\n').Split('\n');
            if (!lines[0].StartsWith("### ")) { list.Problems.Add("text in Open questions outside a '### <id>' block"); continue; }
            var id = lines[0][4..].Trim();
            if (id == "Dismissed")
            {
                list.Dismissed.AddRange(lines.Skip(1).Where(l => l.StartsWith("- ")).Select(l => l[2..].Trim()));
                continue;
            }
            var q = new Question { Id = id };
            var seen = new HashSet<string>();
            for (var i = 1; i < lines.Length; i++)
            {
                var m = Regex.Match(lines[i], @"^- (State|Raised by|Attempts|Missing|Waiting on|Answer looks like|Answer):\s*(.*)$");
                if (!m.Success) { if (lines[i].Trim().Length > 0) list.Problems.Add($"question {id}: unexpected line '{lines[i].Trim()}'"); continue; }
                var value = m.Groups[2].Value.Trim();
                seen.Add(m.Groups[1].Value);
                switch (m.Groups[1].Value)
                {
                    case "State": q.State = value; break;
                    case "Raised by": q.RaisedBy = value; break;
                    case "Attempts": if (!int.TryParse(value, out q.Attempts)) list.Problems.Add($"question {id}: Attempts is not a number"); break;
                    case "Missing": q.Missing = value; break;
                    case "Waiting on": q.WaitingOn = value; break;
                    case "Answer looks like": q.AnswerLooksLike = value; break;
                    case "Answer":
                        // A person may type several lines; everything after "Answer:" is the answer.
                        q.Answer = string.Join('\n', new[] { value }.Concat(lines.Skip(i + 1))).Trim();
                        i = lines.Length;
                        break;
                }
            }
            foreach (var required in new[] { "State", "Raised by", "Missing", "Waiting on", "Answer looks like" })
                if (!seen.Contains(required)) list.Problems.Add($"question {id}: '{required}' is missing");
            if (!Question.States.Contains(q.State)) list.Problems.Add($"question {id}: state '{q.State}' is not one of {string.Join(", ", Question.States)}");
            if (!Question.Raisers.Contains(q.RaisedBy)) list.Problems.Add($"question {id}: raised by '{q.RaisedBy}' is not one of {string.Join(", ", Question.Raisers)}");
            if (!Regex.IsMatch(q.WaitingOn, @"^(person|(repository|name|entry) \S.*)$")) list.Problems.Add($"question {id}: 'Waiting on' must be 'person', 'repository <id>', 'name <provided name>' or 'entry <id>'");
            if (list.Items.Any(o => o.Id == q.Id)) list.Problems.Add($"question {id}: the id is used twice");
            if (list.Items.Any(o => o.Key == q.Key)) list.Problems.Add($"question {id}: duplicates another question on this entry");
            list.Items.Add(q);
        }
        return list;
    }

    public string Render()
    {
        var sb = new StringBuilder();
        foreach (var q in Items)
            sb.Append($"### {q.Id}\n\n- State: {q.State}\n- Raised by: {q.RaisedBy}\n- Attempts: {q.Attempts}\n- Missing: {q.Missing}\n- Waiting on: {q.WaitingOn}\n- Answer looks like: {q.AnswerLooksLike}\n- Answer:{(q.Answer.Length > 0 ? " " + q.Answer : "")}\n\n");
        if (Dismissed.Count > 0) sb.Append("### Dismissed\n\n" + string.Join('\n', Dismissed.Select(d => "- " + d)) + "\n");
        return sb.ToString().Trim('\n');
    }

    /// <summary>Adds a question unless the same missing fact is already asked or dismissed. Returns the question, or null for a duplicate.</summary>
    public Question? Add(string missing, string waitingOn, string answerLooksLike, string raisedBy)
    {
        var q = new Question { Missing = OneLine(missing), WaitingOn = OneLine(waitingOn), AnswerLooksLike = OneLine(answerLooksLike), RaisedBy = raisedBy, State = waitingOn == "person" ? "needs-person" : "open" };
        if (Items.Any(o => o.Key == q.Key)) return null;
        var n = 1;
        while (Items.Any(o => o.Id == $"q{n}")) n++;
        q.Id = $"q{n}";
        Items.Add(q);
        return q;
    }

    public static void Update(Entry entry, Action<QuestionList> change)
    {
        var list = Parse(entry.Body);
        change(list);
        entry.Body = Sections.Set(entry.Body, Sections.Questions, list.Render());
    }

    static string OneLine(string s) => Regex.Replace(s, @"\s+", " ").Trim();
}
