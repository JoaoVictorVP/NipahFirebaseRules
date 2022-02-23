using Newtonsoft.Json;
using System.Text;

namespace NipahFirebaseRules.RuleScript;

public class RSSyntaxNode
{
    public virtual RSSyntaxKind Kind { get; }
    public RSSyntaxNode Parent { get; set; }
    public RSSyntaxNode Root
    {
        get
        {
            var root = this;
            begin:
            if(root.Parent != null)
            {
                root = root.Parent;
                goto begin;
            }
            return root;
        }
    }
    public readonly List<RSSyntaxNode> Children = new List<RSSyntaxNode>(32);

    public virtual void BuildJSAsTree(StringBuilder code)
    {
        foreach(var child in Children)
        {
            code.Append("root");
            child.BuildJS(code);
            code.Append(";");
        }
    }

    public virtual void BuildJS(StringBuilder code)
    {

    }

    protected void BuildChildren(StringBuilder code)
    {
        foreach (var child in Children)
            child.BuildJS(code);
    }

    public static void ResetLevel() => level = 0;

    static int level;
    protected static void LevelUp() => level++;
    protected static void LevelDown(StringBuilder code)
    {
        code.Append(".End()");
        level--;
    }
    protected static void LevelToZero(StringBuilder code)
    {
        code.AppendLine(".ToRoot()");
        level = 0;
    }

    public RSSyntaxNode AddChild(RSSyntaxNode child)
    {
        Children.Add(child);
        child.Parent = this;
        return this;
    }
    public TChild AddChildAndEnter<TChild>(TChild child) where TChild : RSSyntaxNode
    {
        Children.Add(child);
        child.Parent = this;
        return child;
    }
}

// Variable
public class RSVariable : RSSyntaxNode
{
    public override RSSyntaxKind Kind => RSSyntaxKind.Variable;

    public string? Name;
    public dynamic? Value;

    public override void BuildJS(StringBuilder code)
    {
        string json = JsonConvert.SerializeObject(Value);
        code.AppendLine($".Define(\"{Name}\", {json})");
    }
}

// Expression
public class RSExpression : RSSyntaxNode
{
    public string Expression = "";
    public bool Literal;

    public override void BuildJS(StringBuilder code)
    {
        code.AppendLine($".Expression(\"{Expression}\", {(Literal ? "true" : "false")})");
    }
}

// Invoke
public class RSInvoke : RSSyntaxNode
{
    public override RSSyntaxKind Kind => RSSyntaxKind.Invoke;

    public RSValue? Intent;

    public override void BuildJS(StringBuilder code)
    {
        code.AppendLine(Intent.ToJS());
    }
}

// Match
public class RSMatch : RSSyntaxNode
{
    public override RSSyntaxKind Kind => RSSyntaxKind.Match;

    public string Name;

    public override void BuildJS(StringBuilder code)
    {
        code.AppendLine($".Match(\"{Name}\")");
        LevelUp();
        BuildChildren(code);
        LevelDown(code);
    }
}

// Write, Read
public class RSWriteRead : RSSyntaxNode
{
    public override RSSyntaxKind Kind => Write ? RSSyntaxKind.Write : RSSyntaxKind.Read;
    public bool Write;

    public override void BuildJS(StringBuilder code)
    {
        code.AppendLine($".{(Write? "Write" : "Read")}()");
        LevelUp();
        BuildChildren(code);
        LevelDown(code);
    }
}

// If
public class RSIf : RSSyntaxNode
{
    public override RSSyntaxKind Kind => RSSyntaxKind.Comparison;

    public IfRule.IfType Type;
    public RSValue? Left;
    public RSValue? Right;

    public NextIf Next;
    public RSIf? NextComparison;

    public override void BuildJS(StringBuilder code)
    {
        code.AppendLine($".If(Comparison.{Type})");
        code.AppendLine($"{Left.ToJS()}.AsLeft()");
        code.AppendLine($"{Right.ToJS()}.AsRight()");
        LevelUp();
        if(Next != NextIf.None)
        {
            LevelDown(code);
            code.AppendLine(Next switch { NextIf.And => ".And()", NextIf.Or => ".Or()" });
            NextComparison!.BuildJS(code);
        }else
            LevelDown(code);
    }

    public enum NextIf
    {
        None,
        And,
        Or
    }
}

// Value
public class RSValue : RSSyntaxNode
{
    public override RSSyntaxKind Kind => RSSyntaxKind.Value;

    public dynamic Value;
    public VType Type;

    public string ToJS() => Type switch
    {
        VType.Literal => $".Value({formatLiteral()})",
        VType.Variable => $".Variable(\"{Value}\")",
        VType.Function => $".Invoke(\"{Value}\")",

        VType.Expression => $".Expression(\"{Value.Item1}\", {(Value.Item2? "true" : "false")})"
    };

    dynamic formatLiteral()
    {
        if (Value is string)
            return '"' + Value + '"';
        if (Value is bool)
            return Value ? "true" : "false";
        return Value;
    }

    public override void BuildJS(StringBuilder code)
    {
        code.AppendLine(ToJS());
    }

    public enum VType
    {
        Literal,
        Variable,
        Function,

        Expression
    }
}