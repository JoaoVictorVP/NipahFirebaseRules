using Jint.Native;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Dynamic;

using ExpandoObject = System.Collections.Generic.Dictionary<string, dynamic>;

namespace NipahFirebaseRules;

public abstract class Rule
{
    public RuleBuilder Builder { get; private set; }
    public Rule? Parent;
    public List<Rule> Children = new List<Rule>(32);

    public Rule AddFunction(string name, MainRule function)
    {
        ToRoot().Functions.Add((name, function));
        return this;
    }

    public Rule Define(string varName, dynamic varValue)
    {
        ToRoot().Variables[varName] = varValue;
        //ToRoot().Variables.Add(varName, varValue);
        return this;
    }

    public Rule Match(string name)
    {
        var m = new MatchRule { Name = name, Parent = this }.SetBuilder(Builder);
        Children.Add(m);
        return m;
    }
    public Rule Write()
    {
        var w = new WriteRule() { Parent = this }.SetBuilder(Builder);
        Children.Add(w);
        return w;
    }
    public Rule Read()
    {
        var r = new ReadRule() { Parent = this }.SetBuilder(Builder);
        Children.Add(r);
        return r;
    }

    public Rule If(IfRule.IfType type = IfRule.IfType.Equality)
    {
        var i = new IfRule { Term = type, Parent = this }.SetBuilder(Builder);
        Children.Add(i);
        return i;
    }

    public Rule Value(dynamic value)
    {
        var v = new ValueRule { Value = value, Parent = this }.SetBuilder(Builder);
        Children.Add(v);
        return this;
    }
    public Rule EnterValue(dynamic value)
    {
        var v = new ValueRule { Value = value, Parent = this }.SetBuilder(Builder);
        Children.Add(v);
        return v;
    }

    public Rule And()
    {
        var and = new AndRule { Parent = this }.SetBuilder(Builder);
        Children.Add(and);
        return this;
    }
    public Rule Or()
    {
        var or = new OrRule { Parent = this }.SetBuilder(Builder);
        Children.Add(or);
        return this;
    }

    public Rule Variable(string name)
    {
        var v = new VariableRule { Name = name, Parent = this }.SetBuilder(Builder);
        Children.Add(v);
        return this;
    }

    public Rule Invoke(string function)
    {
        var inv = new InvokeRule { Function = function, Parent = this }.SetBuilder(Builder);
        Children.Add(inv);
        return this;
    }

    public Rule Expression(string exp, bool literal)
    {
        var expr = new ExpressionRule { Expression = exp, Literal = literal, Parent = this }.SetBuilder(Builder);
        Children.Add(expr);
        return this;
    }

    public Rule End() => Parent;

    public BinaryRule AsLeft()
    {
        var self = this as BinaryRule;
        var c = Children[0];
        Children.RemoveAt(0);
        self.Left = c;

        return self;
    }
    public BinaryRule AsRight()
    {
        var self = this as BinaryRule;
        var c = Children[0];
        Children.RemoveAt(0);
        self.Right = c;

        return self;
    }

    public MainRule ToRoot()
    {
        var root = this;
    begin:
        if (root is MainRule main)
            return main;
        else
        {
            root = root!.Parent;
            goto begin;
        }
    }

    public dynamic Build()
    {
        var root = ToRoot();
        dynamic rules = new ExpandoObject();
        root.ProduceJSON(rules, ref rules);

        return rules;
    }

    public abstract void ProduceJSON(dynamic rules, ref dynamic parent);

    public virtual bool TestSelf(TestOperation op, dynamic value)
    {
        if (this is WriteRule && op == TestOperation.Read) return true;
        else if (this is ReadRule && op == TestOperation.Write) return true;

        bool skipNext = false;
        foreach (var child in Children)
        {
            if(child is AndRule || child is OrRule)
            {
                skipNext = true;
                continue;
            }

            if(skipNext)
            {
                skipNext = false;
                continue;
            }

            if (!child.TestSelf(op, value))
                return false;
        }

        return true;
    }

    public Rule SetBuilder(RuleBuilder builder)
    {
        Builder = builder;
        return this;
    }
}

public class ExpressionRule : Rule
{
    public string Expression;
    public bool Literal;

    public override void ProduceJSON(dynamic rules, ref dynamic parent)
    {
        parent = Expression;
        //parent = Literal ? Expression.Remove(0, 1).Remove(Expression.Length - 2) : Expression;
    }
    public override bool TestSelf(TestOperation op, dynamic value)
    {
        return true;
    }
}

public class MatchRule : Rule
{
    public string Name;

    public override void ProduceJSON(dynamic rules, ref dynamic parent)
    {
        dynamic body = new ExpandoObject();

        foreach (var child in Children)
            child.ProduceJSON(rules, ref body);

        parent[Name] = body;
    }
}
public class WriteRule : Rule
{
    public override void ProduceJSON(dynamic rules, ref dynamic parent)
    {
        dynamic write = new ExpandoObject();
        foreach (var child in Children)
            child.ProduceJSON(rules, ref write);
        parent[".write"] = write;
    }
}
public class ReadRule : Rule
{
    public override void ProduceJSON(dynamic rules, ref dynamic parent)
    {
        dynamic read = new ExpandoObject();
        foreach (var child in Children)
            child.ProduceJSON(rules, ref read);
        parent[".read"] = read;
    }
}

public abstract class BinaryRule : Rule
{
    public Rule Left;
    public Rule Right;
}
public abstract class BinaryRule<Middle> : BinaryRule
{
    public Middle Term;
}

public class IfRule : BinaryRule<IfRule.IfType>
{
    public override void ProduceJSON(dynamic rules, ref dynamic parent)
    {
        string comparison;
        switch(Term)
        {
            case IfType.Equality: comparison = "==="; break;
            case IfType.Difference: comparison = "!=="; break;
            case IfType.GreaterThan: comparison = ">"; break;
            case IfType.GreaterThanOrEqual: comparison = ">="; break;
            case IfType.LowerThan: comparison = "<"; break;
            case IfType.LowerThanOrEqual: comparison = "<="; break;
            default: comparison = "=="; break;
        }

        dynamic left = new ExpandoObject();
        Left.ProduceJSON(rules, ref left);
        dynamic right = new ExpandoObject();
        Right.ProduceJSON(rules, ref right);

        bool lexp = Left is ExpressionRule lexpr && lexpr.Literal;
        bool rexp = Right is ExpressionRule rexpr && rexpr.Literal;

        bool lfun = Left is InvokeRule;
        bool rfun = Right is InvokeRule;

        if(!(left is string sleft && sleft[0] == '$') && !lexp && !lfun)
            left = JsonConvert.SerializeObject(left).Replace('"', '\'');
        if(!(right is string sright && sright[0] == '$') && !rexp && !rfun)
            right = JsonConvert.SerializeObject(right).Replace('"', '\'');

        /*if (left is string && left[0] == '$')
            left = "'" + left + "'";
        if (right is string && right[0] == '$')
            right = "'" + right + "'";*/

        if (right is bool b == true && Term == IfType.Equality)
        {
            if (parent is string)
                parent += $"{left}";
            else
                parent = $"{left}";
        }
        else
        {
            if (parent is string)
                parent += $"({left} {comparison} {right})";
            else
                parent = $"({left} {comparison} {right})";
        }
    }

    public override bool TestSelf(TestOperation op, dynamic value)
    {
        var self = testSelf(op, value);

        if(Parent != null)
        {
            int index = Parent.Children.IndexOf(this);
            if(index < Parent.Children.Count - 1)
            {
                var next = Parent.Children[index + 1];
                var other = Parent.Children[index + 2];

                if (next is AndRule)
                    return self && other.TestSelf(op, value);
                else if (next is OrRule)
                    return self || other.TestSelf(op, value);
            }
        }
        return self;
    }
    bool testSelf(TestOperation op, dynamic value)
    {
        dynamic left = Left is ValueRule lvalue ? lvalue.Value! : 
            Left is VariableRule lvar ? lvar.Value! : Left!.TestSelf(op, value);
        dynamic right = Right is ValueRule rvalue ? rvalue.Value! : 
            Right is VariableRule rvar ? rvar.Value! : Right!.TestSelf(op, value);

        switch (Term)
        {
            case IfType.Equality: return left == right;
            case IfType.Difference: return left != right;
            case IfType.GreaterThan: return left > right;
            case IfType.LowerThan: return left < right;
            case IfType.GreaterThanOrEqual: return left >= right;
            case IfType.LowerThanOrEqual: return left <= right;
            default: return false;
        }
    }

    public enum IfType
    {
        Equality,
        Difference,
        GreaterThan,
        LowerThan,
        GreaterThanOrEqual,
        LowerThanOrEqual
    }
}

public class AndRule : Rule
{
    public override void ProduceJSON(dynamic rules, ref dynamic parent)
    {
        parent += " && ";
    }
}
public class OrRule : Rule
{
    public override void ProduceJSON(dynamic rules, ref dynamic parent)
    {
        parent += " || ";
    }
}

public class InvokeRule : Rule
{
    public string Function;

    public override void ProduceJSON(dynamic rules, ref dynamic parent)
    {
        var fun = ToRoot().Functions.Find(f => f.name == Function).function;

        dynamic exp = new ExpandoObject();
        fun.ProduceJSON(exp, ref exp);
        string json = JsonConvert.SerializeObject(exp);
        //if (json[0] == '"' && json[json.Length - 1] == '"')
        //    json = json.Remove(0, 1).Remove(json.Length - 1, 1);

        if (json[0] == '"' && json[json.Length - 1] == '"')
            json = json.Remove(0, 1).Remove(json.Length - 2);

        json = json.Replace('"', '\'');

        parent = json;
    }

    public override bool TestSelf(TestOperation op, dynamic value)
    {
        var fun = ToRoot().Functions.Find(f => f.name == Function).function;

        return fun.TestSelf(op, value);
    }
}

public class VariableRule : Rule
{
    public string Name;
    public dynamic Value => Name[0] == '$' ? Name : ToRoot().Variables[Name];

    public override void ProduceJSON(dynamic rules, ref dynamic parent)
    {
        dynamic value = Value;
        //if (value is string)
        //    parent = '"' + value + '"';
        //else
            parent = value;
    }
}

public class ValueRule : Rule
{
    public dynamic? Value;

    public override void ProduceJSON(dynamic rules, ref dynamic parent)
    {
        //if (Value is string)
        //    parent = '"' + Value + '"';
        //else
            parent = Value;
    }
}