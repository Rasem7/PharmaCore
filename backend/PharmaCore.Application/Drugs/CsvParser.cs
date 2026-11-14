using System.Text;

namespace PharmaCore.Application.Drugs;

/// <summary>Minimal RFC 4180 reader: quoted fields, escaped quotes (""), embedded commas and newlines.</summary>
public static class CsvParser
{
    public static IEnumerable<string[]> Parse(TextReader reader)
    {
        var field = new StringBuilder();
        var row = new List<string>();
        var inQuotes = false;
        int c;
        while ((c = reader.Read()) != -1)
        {
            var ch = (char)c;
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (reader.Peek() == '"') { reader.Read(); field.Append('"'); }
                    else inQuotes = false;
                }
                else field.Append(ch);
            }
            else if (ch == '"' && field.Length == 0) inQuotes = true;
            else if (ch == ',') { row.Add(field.ToString()); field.Clear(); }
            else if (ch == '\r') { /* handled with \n */ }
            else if (ch == '\n') { row.Add(field.ToString()); field.Clear(); yield return row.ToArray(); row.Clear(); }
            else field.Append(ch);
        }
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); yield return row.ToArray(); }
    }
}
