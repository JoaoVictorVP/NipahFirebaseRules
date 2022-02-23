using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ExpandoObject = System.Collections.Generic.Dictionary<string, object>;

namespace NipahFirebaseRules;

public class RuleBuilder
{
    public Rule Root() => new MainRule().SetBuilder(this);
}
public class MainRule : Rule
{
    public static ExpandoObject GVariables = new ExpandoObject(32);
    public ExpandoObject Variables => GVariables;
    public List<(string name, MainRule function)> Functions = new (32);

    public bool Test(TestOperation operation, dynamic value, params string[] path)
    {
        Queue<string> spath = new (path);
        Rule searchIn = this;
        search:
        if(spath.Count > 0)
        {
            var searched = lastMatch(searchIn, spath.Dequeue());
            if(searched != null)
            {
                searchIn = searched;
                goto search;
            }
        }

        if (searchIn == null) return false;

        return verifyRule(searchIn, operation, value);
    }
    bool verifyRule(Rule verify, TestOperation operation, dynamic value)
    {
        foreach (var child in verify.Children)
            if (!child.TestSelf(operation, value))
                return false;
        return true;
    }
    MatchRule lastMatch(Rule search, string path)
    {
        foreach(var child in search.Children)
        {
            if(child is MatchRule match)
            {
                if (match.Name == path)
                    return match;
            }
            else if(child.Children.Count > 0)
            {
                var find = lastMatch(child, path);
                if (find != null && find.Name == path)
                    return find;
            }
        }
        return null;
    }

    public override void ProduceJSON(dynamic rules, ref dynamic parent)
    {
        foreach (var child in Children)
            child.ProduceJSON(rules, ref parent);
    }
}
public enum TestOperation
{
    Write,
    Read
}