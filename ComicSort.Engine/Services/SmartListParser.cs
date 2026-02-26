using ComicSort.Engine.Models;
using System.Globalization;
using System.Text;

namespace ComicSort.Engine.Services;

public sealed class SmartListParser : ISmartListParser
{
    public bool TryParse(string? queryText, out MatcherGroupNode expression, out string? error)
    {
        expression = new MatcherGroupNode();
        error = null;

        if (string.IsNullOrWhiteSpace(queryText))
        {
            return false;
        }

        var tokenizer = new Tokenizer(queryText);

        try
        {
            expression = ParseGroup(tokenizer, requireMatchKeyword: true);
            tokenizer.Expect(TokenType.End);
            return true;
        }
        catch (ParseException ex)
        {
            error = ex.Message;
            expression = new MatcherGroupNode();
            return false;
        }
    }

    private static MatcherGroupNode ParseGroup(Tokenizer tokenizer, bool requireMatchKeyword)
    {
        if (requireMatchKeyword)
        {
            tokenizer.ExpectWord("Match");
        }

        var modeWord = tokenizer.Expect(TokenType.Word).Text;
        var group = new MatcherGroupNode
        {
            Mode = string.Equals(modeWord, "Any", StringComparison.OrdinalIgnoreCase)
                ? MatcherMode.Or
                : MatcherMode.And
        };

        tokenizer.ExpectSymbol("(");

        while (!tokenizer.PeekSymbol(")"))
        {
            var node = ParseNode(tokenizer);
            group.Children.Add(node);

            if (tokenizer.PeekSymbol(";"))
            {
                tokenizer.ExpectSymbol(";");
                continue;
            }

            if (tokenizer.PeekWord("And") || tokenizer.PeekWord("Or"))
            {
                tokenizer.Expect(TokenType.Word);
            }
        }

        tokenizer.ExpectSymbol(")");
        return group;
    }

    private static IMatcherNode ParseNode(Tokenizer tokenizer)
    {
        var not = tokenizer.TryConsumeWord("Not");

        if (tokenizer.PeekWord("Match"))
        {
            var group = ParseGroup(tokenizer, requireMatchKeyword: true);
            group.Not = not;
            return group;
        }

        var rule = ParseRule(tokenizer);
        rule.Not = not;
        return rule;
    }

    private static MatcherRuleNode ParseRule(Tokenizer tokenizer)
    {
        tokenizer.ExpectSymbol("[");

        var fieldBuilder = new StringBuilder();
        while (!tokenizer.PeekSymbol("]"))
        {
            var token = tokenizer.ExpectOneOf(TokenType.Word, TokenType.String);
            if (fieldBuilder.Length > 0)
            {
                fieldBuilder.Append(' ');
            }

            fieldBuilder.Append(token.Text);
        }

        tokenizer.ExpectSymbol("]");

        var opText = ParseOperatorText(tokenizer);
        var value1 = ParseOptionalValue(tokenizer);
        var value2 = ParseOptionalSecondValue(tokenizer);

        var field = SmartListNodeMapper.ParseField(fieldBuilder.ToString());
        var op = SmartListNodeMapper.ParseOperator(opText);
        var valueKind = InferValueKind(field, op, value1, value2);

        return new MatcherRuleNode
        {
            Field = field,
            Operator = op,
            Value1 = value1,
            Value2 = value2,
            ValueKind = valueKind
        };
    }

    private static string? ParseOptionalValue(Tokenizer tokenizer)
    {
        var token = tokenizer.PeekToken();
        if (!IsValueToken(token))
        {
            return null;
        }

        return tokenizer.ExpectOneOf(TokenType.String, TokenType.Word).Text;
    }

    private static string? ParseOptionalSecondValue(Tokenizer tokenizer)
    {
        if (tokenizer.PeekWord("to") || tokenizer.PeekWord("and"))
        {
            tokenizer.Expect(TokenType.Word);
        }

        var token = tokenizer.PeekToken();
        if (!IsValueToken(token))
        {
            return null;
        }

        return tokenizer.ExpectOneOf(TokenType.String, TokenType.Word).Text;
    }

    private static bool IsValueToken(Token token)
    {
        return token.Type is TokenType.String or TokenType.Word;
    }

    private static string ParseOperatorText(Tokenizer tokenizer)
    {
        if (!tokenizer.Peek(TokenType.Word))
        {
            return "contains";
        }

        var first = tokenizer.Expect(TokenType.Word).Text;

        if (string.Equals(first, "contains", StringComparison.OrdinalIgnoreCase))
        {
            if (tokenizer.TryConsumeWord("any"))
            {
                tokenizer.TryConsumeWord("of");
                return "contains any of";
            }

            if (tokenizer.TryConsumeWord("all"))
            {
                tokenizer.TryConsumeWord("of");
                return "contains all of";
            }

            return "contains";
        }

        if (string.Equals(first, "starts", StringComparison.OrdinalIgnoreCase) && tokenizer.TryConsumeWord("with"))
        {
            return "starts with";
        }

        if (string.Equals(first, "ends", StringComparison.OrdinalIgnoreCase) && tokenizer.TryConsumeWord("with"))
        {
            return "ends with";
        }

        if (string.Equals(first, "list", StringComparison.OrdinalIgnoreCase) && tokenizer.TryConsumeWord("contains"))
        {
            return "list contains";
        }

        if (string.Equals(first, "regular", StringComparison.OrdinalIgnoreCase) && tokenizer.TryConsumeWord("expression"))
        {
            return "regular expression";
        }

        if (string.Equals(first, "is", StringComparison.OrdinalIgnoreCase))
        {
            if (tokenizer.TryConsumeWord("in"))
            {
                tokenizer.TryConsumeWord("the");
                if (tokenizer.TryConsumeWord("range"))
                {
                    return "is in the range";
                }
            }

            if (tokenizer.TryConsumeWord("greater"))
            {
                return "is greater";
            }

            if (tokenizer.TryConsumeWord("smaller"))
            {
                return "is smaller";
            }

            if (tokenizer.TryConsumeWord("yes"))
            {
                return "is Yes";
            }

            if (tokenizer.TryConsumeWord("no"))
            {
                return "is No";
            }

            if (tokenizer.TryConsumeWord("unknown"))
            {
                return "is Unknown";
            }

            return "is";
        }

        return first;
    }

    private static MatcherValueKind InferValueKind(
        MatcherField field,
        MatcherOperator op,
        string? value1,
        string? value2)
    {
        if (op is MatcherOperator.IsYes or MatcherOperator.IsNo or MatcherOperator.IsUnknown)
        {
            return MatcherValueKind.Boolean;
        }

        if (field is MatcherField.SizeBytes or MatcherField.Year)
        {
            return MatcherValueKind.Number;
        }

        if (field is MatcherField.Added or MatcherField.Modified or MatcherField.LastScanned)
        {
            return MatcherValueKind.Date;
        }

        if (double.TryParse(value1, NumberStyles.Float, CultureInfo.InvariantCulture, out _) &&
            (string.IsNullOrWhiteSpace(value2) || double.TryParse(value2, NumberStyles.Float, CultureInfo.InvariantCulture, out _)))
        {
            return MatcherValueKind.Number;
        }

        return MatcherValueKind.String;
    }

    private enum TokenType
    {
        Word,
        String,
        Symbol,
        End
    }

    private sealed class Tokenizer
    {
        private readonly IReadOnlyList<Token> _tokens;
        private int _index;

        public Tokenizer(string input)
        {
            _tokens = Tokenize(input);
            _index = 0;
        }

        public Token PeekToken()
        {
            return _tokens[_index];
        }

        public bool Peek(TokenType tokenType)
        {
            return PeekToken().Type == tokenType;
        }

        public bool PeekWord(string expected)
        {
            var token = PeekToken();
            return token.Type == TokenType.Word &&
                   string.Equals(token.Text, expected, StringComparison.OrdinalIgnoreCase);
        }

        public bool PeekSymbol(string expected)
        {
            var token = PeekToken();
            return token.Type == TokenType.Symbol && token.Text == expected;
        }

        public bool TryConsumeWord(string expected)
        {
            if (!PeekWord(expected))
            {
                return false;
            }

            _index++;
            return true;
        }

        public Token Expect(TokenType tokenType)
        {
            var token = PeekToken();
            if (token.Type != tokenType)
            {
                throw new ParseException($"Expected {tokenType} at position {token.Position}.");
            }

            _index++;
            return token;
        }

        public Token ExpectOneOf(TokenType first, TokenType second)
        {
            var token = PeekToken();
            if (token.Type != first && token.Type != second)
            {
                throw new ParseException($"Expected {first} or {second} at position {token.Position}.");
            }

            _index++;
            return token;
        }

        public void ExpectWord(string expected)
        {
            var token = Expect(TokenType.Word);
            if (!string.Equals(token.Text, expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new ParseException($"Expected '{expected}' at position {token.Position}.");
            }
        }

        public void ExpectSymbol(string symbol)
        {
            var token = Expect(TokenType.Symbol);
            if (token.Text != symbol)
            {
                throw new ParseException($"Expected '{symbol}' at position {token.Position}.");
            }
        }

        private static IReadOnlyList<Token> Tokenize(string input)
        {
            var tokens = new List<Token>();
            var length = input.Length;
            var position = 0;

            while (position < length)
            {
                var current = input[position];

                if (char.IsWhiteSpace(current))
                {
                    position++;
                    continue;
                }

                if (current is '(' or ')' or '[' or ']' or ';')
                {
                    tokens.Add(new Token(TokenType.Symbol, current.ToString(), position));
                    position++;
                    continue;
                }

                if (current == '"')
                {
                    position++;
                    var start = position;
                    var builder = new StringBuilder();

                    while (position < length)
                    {
                        var ch = input[position];
                        if (ch == '\\' && position + 1 < length)
                        {
                            builder.Append(input[position + 1]);
                            position += 2;
                            continue;
                        }

                        if (ch == '"')
                        {
                            break;
                        }

                        builder.Append(ch);
                        position++;
                    }

                    if (position >= length || input[position] != '"')
                    {
                        throw new ParseException($"Unterminated string literal at position {start}.");
                    }

                    position++;
                    tokens.Add(new Token(TokenType.String, builder.ToString(), start));
                    continue;
                }

                var wordStart = position;
                var wordBuilder = new StringBuilder();
                while (position < length)
                {
                    var ch = input[position];
                    if (char.IsWhiteSpace(ch) || ch is '(' or ')' or '[' or ']' or ';')
                    {
                        break;
                    }

                    wordBuilder.Append(ch);
                    position++;
                }

                tokens.Add(new Token(TokenType.Word, wordBuilder.ToString(), wordStart));
            }

            tokens.Add(new Token(TokenType.End, string.Empty, length));
            return tokens;
        }
    }

    private sealed record Token(TokenType Type, string Text, int Position);

    private sealed class ParseException : Exception
    {
        public ParseException(string message) : base(message)
        {
        }
    }
}
