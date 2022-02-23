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
    RSSyntaxNode buildTree(Tokens tokens)
    {
        var root = new RSSyntaxNode();
        begin:
        var token = tokens.Next();
        root.Children.Add(buildNode(tokens, token));
        if (tokens.Look_Next() != TokenType.None)
            goto begin;

        return root;
    }
    RSSyntaxNode buildNode(Tokens tokens, Token token)
    {
        if (token == "match")
        {
            RSMatch? match = null;

        begin:
            token = tokens.Look_Next();
            if (token == TokenType.OpenBrackets)
                return match;
            else
            {
                string name = null;
                if (token == TokenType.Rich)
                {
                    name = "$";
                    tokens.Next();
                    token = tokens.Look_Next();
                }
                name += token.value;
                tokens.Next();

                if (match == null)
                    match = new RSMatch { Name = name };
                else
                    match = match.AddChildAndEnter(new RSMatch { Name = name });
                //match.Children.Add(match = new RSMatch { Name = name });

                token = tokens.Look_Next();
                if (token == TokenType.Divide)
                {
                    tokens.Next();
                    goto begin;
                }
            }

            token = tokens.Next();
            buildBlock(match, tokens, token);

            return match.Root;
        }
        else if (token == "if")
            return buildIf(tokens, token);
        else if (token == "write")
            return buildWriteRead(true, tokens, token);
        else if (token == "read")
            return buildWriteRead(false, tokens, token);
        else if (token == "invoke")
            return buildInvoke(tokens, token);
        else if (token == "var" || token == "let" || token == "const")
            return buildVariable(tokens, token);
        else if (token == "exp" || token == "expr" || token == "expression")
            return buildExpression(tokens, token);
        else
            return buildValue(tokens, token);

        return null;
    }

    RSExpression buildExpression(Tokens tokens, Token token)
    {
        var exp = getExpression(tokens, token);
        return new RSExpression { Expression = exp.code, Literal = exp.literal };
    }
    (string code, bool literal) getExpression(Tokens tokens, Token token)
    {
        bool literal = false;
        if(tokens.Look_Next() == TokenType.Exclamation)
        {
            tokens.Next();
            literal = true;
        }

        tokens.Next().Assert(TokenType.OpenParenthesis);

        bool dot = false;

        int closed = 1;

        string exp = "";
    begin:
        token = tokens.Next();

        if (token == TokenType.OpenParenthesis)
            closed++;

        if (token == TokenType.Dot)
            dot = true;

        if (token == TokenType.CloseParenthesis && ((closed -= 1) == 0))
            goto end;

        exp += token.text;
        if (tokens.Look_Next() == TokenType.ID)
        {
            if(!dot)
                exp += ' ';
            dot = false;
        }
        goto begin;

    end:
        return (exp, literal);
    }

    RSVariable buildVariable(Tokens tokens, Token token)
    {
        token = tokens.Next();
        string name = token.Value;
        token = tokens.Next();
        token.Assert(TokenType.To);
        token = tokens.Next();
        dynamic value = token.value;

        return new RSVariable { Name = name, Value = value };
    }

    RSInvoke buildInvoke(Tokens tokens, Token token)
    {
        string id = (string)(token = tokens.Next()).value;
        if (tokens.Look_Next() != TokenType.OpenParenthesis)
            throw token.IError($"Expecting '(' after invoke {id}");
        tokens.Next();
        if (tokens.Look_Next() != TokenType.CloseParenthesis)
            throw token.IError("Expecting ')' after '('");
        tokens.Next();

        return new RSInvoke { Intent = new RSValue { Value = id, Type = RSValue.VType.Function } };
    }

    RSWriteRead buildWriteRead(bool write, Tokens tokens, Token token)
    {
        token = tokens.Next();
        var wr = new RSWriteRead { Write = write };
        buildBlock(wr, tokens, token, TokenType.To, TokenType.EOF);

        return wr;
    }

    void buildBlock(RSSyntaxNode block, Tokens tokens, Token token, TokenType openToken = TokenType.OpenBrackets, TokenType closeToken = TokenType.CloseBrackets)
    {
        token.Assert(openToken);

    child:
        var node = buildNode(tokens, tokens.Next());

        block.AddChild(node);
        //block.Children.Add(node);

        if(tokens.Look_Next() == closeToken)
        {
            tokens.Next();
            return;
        }
        goto child;
    }

    RSIf buildIf(Tokens tokens, Token token)
    {
        var left = buildValue(tokens, token);
        token = tokens.Next();
        IfRule.IfType comparison = token.type switch
        {
            TokenType.Equal => IfRule.IfType.Equality,
            TokenType.Different => IfRule.IfType.Difference,
            TokenType.Larger => IfRule.IfType.GreaterThan,
            TokenType.Lower => IfRule.IfType.LowerThan,
            TokenType.LargerOrEqual => IfRule.IfType.GreaterThanOrEqual,
            TokenType.LowerOrEqual => IfRule.IfType.LowerThanOrEqual,
            _ => throw token.IError("Expecting one of these [==, !=, >, >=, <, <=]")
        };
        var right = buildValue(tokens, token);

        token = tokens.Look_Next();
        if(token == "and" || token == "or")
        {
            RSIf.NextIf next = token.text switch { "and" => RSIf.NextIf.And, "or" => RSIf.NextIf.Or, _ => throw token.IError("Expecting [and, or]") };

            token = tokens.Next();
            var nextIf = buildIf(tokens, token);

            return new RSIf
            {
                Left = left,
                Type = comparison,
                Right = right,

                Next = next,
                NextComparison = nextIf
            };
        }

        return new RSIf { Left = left, Type = comparison, Right = right };
    }
    RSValue buildValue(Tokens tokens, Token token)
    {
        if(!(token == TokenType.TrueLiteral || token == TokenType.FalseLiteral || token == TokenType.FloatLiteral || token == TokenType.IntegerLiteral || token == TokenType.NullLiteral || token == TokenType.StringLiteral || token == TokenType.StringLiteral))
            token = tokens.Next();
        RSValue value;
        if (token == TokenType.Rich)
            value = new RSValue { Value = '$' + (string)(token = tokens.Next()).value, Type = RSValue.VType.Variable };
        else if (token == TokenType.Email)
            value = new RSValue { Value = (token = tokens.Next()).value, Type = RSValue.VType.Variable };
        else if(token == "exp" || token == "expr" || token == "expression")
        {
            var exp = getExpression(tokens, token);
            value = new RSValue { Value = exp, Type = RSValue.VType.Expression };
        }
        else
        {
            var preview = tokens.Look_Next();
            if (preview == TokenType.OpenParenthesis)
            {
                tokens.Next();
                tokens.Next().Assert(TokenType.CloseParenthesis);

                return new RSValue { Value = token.value, Type = RSValue.VType.Function };
            }

            value = new RSValue { Value = token.value, Type = RSValue.VType.Literal };
        }

        return value;
    }
}
