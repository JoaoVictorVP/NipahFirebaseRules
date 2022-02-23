using NipahTokenizer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Tokens = NipahTokenizer.ProgressiveList<NipahTokenizer.Token>;

namespace NipahFirebaseRules.RuleScript;

public partial class Compiler
{
    Tokenizer tokenizer = new Tokenizer();

    public string TranspileToJs(string script)
    {
        var tokens = new Tokens(tokenizer.Tokenize(script).ilnRemoveAll(t => t.type == TokenType.EOF && t.text != ";"));
        var tree = buildTree(tokens);

        var code = new StringBuilder(320);
        toJs(code, tree);

        return code.ToString();
    }

    void toJs(StringBuilder code, RSSyntaxNode tree)
    {
        RSSyntaxNode.ResetLevel();

        code.AppendLine("function Build(/** @type Rule */ root) {");
        if (tree.Children.Count > 0)
            tree.BuildJSAsTree(code);
            //asJS(code, tree.Children[0]);
        code.Append("\n}");
    }
    //void asJS(StringBuilder code, RSSyntaxNode node) => node.BuildJS(code);
}
